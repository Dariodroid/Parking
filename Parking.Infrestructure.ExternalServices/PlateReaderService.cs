using OpenCvSharp;
using Parking.Application.Contracts;
using Parking.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace Parking.Infrastructure.ExternalServices
{
    /// <summary>Detecta regiones de placa en una imagen y reconoce sus caracteres con OCR.</summary>
    public class PlateReaderService : IPlateService, IDisposable
    {
        private static readonly Regex PlateRegex = new(@"[A-Z]{3}\d{3,4}", RegexOptions.Compiled);
        private readonly string _tessDataPath;
        private readonly Lazy<YoloPlateDetector> _detector;
        private readonly Lazy<TesseractEngine> _engine;

        /// <summary>Obtiene la región central de búsqueda que se presenta como guía al operador.</summary>
        /// <param name="width">Anchura total del fotograma en píxeles.</param>
        /// <param name="height">Altura total del fotograma en píxeles.</param>
        /// <returns>Rectángulo que ocupa el 80 % del ancho y el 60 % de la altura desde su margen.</returns>
        // Coordenadas relativas al frame. La zona se muestra en el visor para orientar la cámara.
        public static OpenCvSharp.Rect GetRecognitionRegion(int width, int height) =>
            new((int)(width * .10), (int)(height * .30), (int)(width * .80), (int)(height * .60));

        /// <summary>Entrega a la interfaz la misma guía del detector sin exponer tipos OpenCV.</summary>
        /// <param name="width">Anchura del fotograma en píxeles.</param>
        /// <param name="height">Altura del fotograma en píxeles.</param>
        /// <returns>Coordenadas de la zona de reconocimiento.</returns>
        public PlateRegion GetRecognitionRegionCoordinates(int width, int height)
        {
            // La conversión conserva exactamente la zona usada por el OCR.
            var region = GetRecognitionRegion(width, height);
            return new PlateRegion(region.X, region.Y, region.Width, region.Height);
        }

        /// <summary>Prepara las rutas y difiere la carga del detector y del OCR hasta la primera lectura.</summary>
        public PlateReaderService()
        {
            _tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Model", "rfdetr_alpr.onnx");

            // El modelo y Tesseract se cargan durante la primera lectura, en
            // Task.Run. Así el visor puede abrirse sin esperar su inicialización.
            _detector = new Lazy<YoloPlateDetector>(() => new RfdetrPlateDetector(modelPath));
            _engine = new Lazy<TesseractEngine>(() =>
            {
                var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
                engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
                return engine;
            });
        }

        /// <summary>Reconoce una placa de un fotograma mediante la variante que también entrega regiones.</summary>
        /// <param name="imageFrame">Imagen codificada de la cámara.</param>
        /// <returns>Placa normalizada, o cadena vacía si no se pudo leer.</returns>
        public async Task<string> RecognizePlateAsync(byte[] imageFrame)
        {
            // Un fotograma ausente no requiere detector ni OCR.
            if (imageFrame == null || imageFrame.Length == 0) return string.Empty;

            // La variante detallada centraliza el procesamiento del fotograma.
            var result = await DetectPlateWithRegionsAsync(imageFrame);
            return result.PlateNumber;
        }

        /// <summary>Busca placas en el fotograma, lee sus caracteres y devuelve solo la región reconocida para el visor.</summary>
        /// <param name="imageFrame">Fotograma codificado que se recibe de la cámara.</param>
        /// <returns>Lectura de placa, recorte y datos de detección; valores vacíos si no pudo reconocerla.</returns>
        public Task<PlateDetectionResult> DetectPlateWithRegionsAsync(byte[] imageFrame) =>
            Task.Run(() => DetectFrame(imageFrame));

        /// <summary>Procesa un fotograma fuera del hilo de WPF y conserva el primer candidato reconocido.</summary>
        private PlateDetectionResult DetectFrame(byte[] imageFrame)
        {
            var result = new PlateDetectionResult();
            try
            {
                if (imageFrame == null || imageFrame.Length == 0) return result;
                using var src = Cv2.ImDecode(imageFrame, ImreadModes.Color);
                if (src.Empty()) return result;

                var regions = FindCandidateRegions(src);
                result.HasPlateCandidates = regions.Count > 0;
                foreach (var rect in regions)
                {
                    var padded = OpenCvSharp.Rect.Intersect(
                        new OpenCvSharp.Rect(rect.X - rect.Width / 20, rect.Y - rect.Height / 8,
                            rect.Width + rect.Width / 10, rect.Height + rect.Height / 4),
                        new OpenCvSharp.Rect(0, 0, src.Width, src.Height));
                    using var plate = new Mat(src, padded);
                    var readings = ReadRegion(plate);
                    if (readings.Count == 0) continue;

                    result.PlateNumber = readings.GroupBy(text => text)
                        .OrderByDescending(group => group.Count()).First().Key;
                    var display = ChooseDisplayRegion(regions, rect);
                    result.DetectedRegions.Add(new PlateRegion(display.X, display.Y, display.Width, display.Height));
                    result.PlateImage = plate.ToBytes(".jpg");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en detección: {ex.Message}");
            }
            return result;
        }

        /// <summary>Traduce las detecciones de la ROI a coordenadas del fotograma completo.</summary>
        private List<OpenCvSharp.Rect> FindCandidateRegions(Mat src)
        {
            var roi = GetRecognitionRegion(src.Width, src.Height);
            using var searchArea = new Mat(src, roi);
            // El modelo se inicializa aquí, fuera del hilo de la interfaz.
            var regions = _detector.Value.Detect(searchArea);
            if (regions.Count == 0) regions = DetectPlateRegions(searchArea);
            return regions.Select(r => new OpenCvSharp.Rect(r.X + roi.X, r.Y + roi.Y, r.Width, r.Height))
                .Select(r => OpenCvSharp.Rect.Intersect(r,
                    new OpenCvSharp.Rect(0, 0, src.Width, src.Height)))
                .Where(r => r.Width >= 60 && r.Height >= 20).Take(3).ToList();
        }

        /// <summary>Prueba las tres franjas y mantiene el consenso de lecturas OCR anterior.</summary>
        private List<string> ReadRegion(Mat plate)
        {
            var readings = new List<string>();
            var engine = _engine.Value;
            lock (engine)
            {
                foreach (double topFraction in new[] { .22, .12, 0.0 })
                {
                    int top = (int)(plate.Height * topFraction);
                    using var band = new Mat(plate,
                        new OpenCvSharp.Rect(0, top, plate.Width, plate.Height - top));
                    foreach (var candidate in CreateOcrCandidates(band))
                        readings.AddRange(ReadPlateFromCandidate(engine, candidate));
                    if (readings.GroupBy(text => text).Any(group => group.Count() >= 3))
                        break;
                }
            }
            return readings;
        }
        /// <summary>Busca por bordes posibles placas cuando el detector principal no encuentra ninguna.</summary>
        /// <param name="src">Zona de búsqueda del fotograma.</param>
        /// <returns>Rectángulos candidatos ordenados por área descendente.</returns>
        private static List<OpenCvSharp.Rect> DetectPlateRegions(Mat src)
        {
            // Se normaliza la iluminación y se resaltan bordes antes de buscar contornos.
            var regions = new List<OpenCvSharp.Rect>();
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            using var normalized = new Mat();
            using (var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8))) clahe.Apply(gray, normalized);
            using var blurred = new Mat(); Cv2.GaussianBlur(normalized, blurred, new Size(5, 5), 0);
            using var edges = new Mat(); Cv2.Canny(blurred, edges, 100, 200);
            Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            // La proporción y anchura descartan contornos que no parecen placas.
            foreach (var contour in contours)
            {
                var rect = Cv2.BoundingRect(contour);
                float aspectRatio = (float)rect.Width / rect.Height;
                if (aspectRatio > 2.2 && aspectRatio < 5.8 && rect.Width > 80)
                    regions.Add(rect);
            }
            return regions.OrderByDescending(r => r.Width * r.Height).ToList();
        }
        /// <summary>Escoge el rectángulo de placa más pequeño contenido en la región que dio una lectura válida.</summary>
        /// <param name="regions">Regiones candidatas detectadas en el fotograma.</param>
        /// <param name="recognizedRegion">Región cuyo recorte permitió reconocer los caracteres.</param>
        /// <returns>La mejor región para dibujar, o la reconocida si no hay una más precisa.</returns>
        private static OpenCvSharp.Rect ChooseDisplayRegion(
            IEnumerable<OpenCvSharp.Rect> regions, OpenCvSharp.Rect recognizedRegion)
        {
            // Una región amplia puede incluir el vehículo entero. Cuando el
            // detector también encontró una caja menor dentro de ella, esa es
            // la que se presenta al operador; el OCR no se modifica.
            return regions
                .Where(candidate => candidate.Width * candidate.Height <= recognizedRegion.Width * recognizedRegion.Height)
                .Where(candidate => recognizedRegion.Contains(
                    new OpenCvSharp.Point(candidate.X + candidate.Width / 2, candidate.Y + candidate.Height / 2)))
                .Where(candidate => (double)candidate.Width / candidate.Height is >= 1.8 and <= 6.0)
                .OrderBy(candidate => candidate.Width * candidate.Height)
                .FirstOrDefault(recognizedRegion);
        }

        /// <summary>Crea versiones de una placa con escalas y contrastes distintos para confirmar el OCR.</summary>
        /// <param name="src">Franja de caracteres extraída de la placa.</param>
        /// <returns>Imágenes PNG alternativas que se leerán con Tesseract.</returns>
        private static List<byte[]> CreateOcrCandidates(Mat src)
        {
            var candidates = new List<byte[]>();
            using var gray = new Mat(); Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            // Una placa grande fotografiada desde una pantalla contiene reflejos y
            // patrones de píxeles. Reducirla antes del OCR conserva los caracteres
            // y atenúa ese ruido; luego usamos un tamaño estable para Tesseract.
            using var normalized = new Mat();
            int targetWidth = Math.Min(gray.Width, 380);
            Cv2.Resize(gray, normalized,
                new Size(targetWidth, Math.Max(1, (int)(gray.Height * (double)targetWidth / gray.Width))),
                0, 0, InterpolationFlags.Area);
            using var enlarged = new Mat();
            Cv2.Resize(normalized, enlarged, new Size(), 2.0, 2.0, InterpolationFlags.Cubic);
            AddCandidate(candidates, enlarged);

            using var contrast = new Mat();
            using (var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8))) clahe.Apply(enlarged, contrast);
            AddCandidate(candidates, contrast);

            // Filtros para mejorar legibilidad
            using var adaptive = new Mat(); Cv2.AdaptiveThreshold(contrast, adaptive, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 31, 11);
            AddCandidate(candidates, adaptive);

            // Segunda escala: los patrones de píxeles de una pantalla cambian al
            // reducirla y permiten confirmar caracteres ambiguos entre variantes.
            if (gray.Width > 300)
            {
                using var smaller = new Mat();
                int smallWidth = Math.Min(gray.Width, 300);
                Cv2.Resize(gray, smaller,
                    new Size(smallWidth, Math.Max(1, (int)(gray.Height * (double)smallWidth / gray.Width))),
                    0, 0, InterpolationFlags.Area);
                using var smallEnlarged = new Mat();
                Cv2.Resize(smaller, smallEnlarged, new Size(), 2.0, 2.0, InterpolationFlags.Cubic);
                using var smallContrast = new Mat();
                using (var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8))) clahe.Apply(smallEnlarged, smallContrast);
                using var smallAdaptive = new Mat();
                Cv2.AdaptiveThreshold(smallContrast, smallAdaptive, 255,
                    AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 31, 11);
                AddCandidate(candidates, smallAdaptive);
            }

            return candidates;
        }

        /// <summary>Agrega una variante PNG cuando la transformación produjo una imagen utilizable.</summary>
        /// <param name="candidates">Lista de variantes para el OCR.</param>
        /// <param name="image">Imagen transformada.</param>
        private static void AddCandidate(ICollection<byte[]> candidates, Mat image)
        {
            // Una matriz vacía no debe enviarse al motor OCR.
            if (!image.Empty()) candidates.Add(image.ToBytes(".png"));
        }

        /// <summary>Lee cada variante con dos modos de segmentación y devuelve placas con formato válido.</summary>
        /// <param name="engine">Motor OCR compartido y protegido por el llamador.</param>
        /// <param name="candidateImage">Imagen PNG de la variante actual.</param>
        /// <returns>Lecturas válidas obtenidas de los modos palabra y línea.</returns>
        private static IEnumerable<string> ReadPlateFromCandidate(TesseractEngine engine, byte[] candidateImage)
        {
            // Los dos modos pueden resolver de forma distinta caracteres difíciles.
            foreach (PageSegMode mode in new[] { PageSegMode.SingleWord, PageSegMode.SingleLine })
            {
                engine.DefaultPageSegMode = mode;
                using var image = Pix.LoadFromMemory(candidateImage);
                using var page = engine.Process(image);
                string text = ExtractPlate(page.GetText());
                if (!string.IsNullOrWhiteSpace(text)) yield return text;
            }
        }

        /// <summary>Normaliza la salida del OCR y extrae una placa del patrón esperado.</summary>
        /// <param name="rawText">Texto sin procesar devuelto por Tesseract.</param>
        /// <returns>Placa con guion tras las tres letras, o cadena vacía.</returns>
        private static string ExtractPlate(string? rawText)
        {
            // Se eliminan separadores y símbolos antes de aplicar la expresión regular.
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
            string compact = Regex.Replace(rawText.ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
            var match = PlateRegex.Match(compact);
            if (!match.Success) return string.Empty;
            string plate = match.Value;
            return plate.Insert(3, "-");
        }

        /// <summary>Implementa la lectura simple de placa reutilizando el reconocedor principal.</summary>
        /// <param name="image">Imagen codificada recibida por la interfaz.</param>
        /// <returns>La tarea de reconocimiento de la placa.</returns>
        public Task<string> DetectPlateAsync(byte[] image)
        {
            return RecognizePlateAsync(image);
        }

        /// <summary>Variante sin imagen mantenida por compatibilidad con la interfaz.</summary>
        /// <returns>No retorna un resultado.</returns>
        /// <exception cref="NotImplementedException">Siempre: no puede reconocerse una placa sin imagen.</exception>
        public Task<string> RecognizePlateAsync()
        {
            // La cámara debe entregar un fotograma a la otra sobrecarga.
            throw new NotImplementedException();
        }

        /// <summary>Libera el motor OCR únicamente si llegó a inicializarse.</summary>
        public void Dispose()
        {
            // La apertura del visor sin lectura no crea recursos de Tesseract.
            if (_engine.IsValueCreated) _engine.Value.Dispose();
        }
    }
}
