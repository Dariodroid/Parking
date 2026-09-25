using OpenCvSharp;
using Parking.Application.EntityService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace Parking.Infrastructure.ExternalServices
{
    public class PlateDetectionResult
    {
        public string PlateNumber { get; set; } = string.Empty;
        public List<OpenCvSharp.Rect> DetectedRegions { get; set; } = new List<OpenCvSharp.Rect>();
        public byte[] PlateImage { get; set; } = Array.Empty<byte>();
        public bool HasDetection => DetectedRegions.Count > 0;
    }

    public class PlateReaderService : IPlateService, IDisposable
    {
        private static readonly Regex PlateRegex = new(@"[A-Z]{3}\d{3,4}", RegexOptions.Compiled);
        private readonly string _tessDataPath;
        private readonly YoloPlateDetector _detector;
        private readonly TesseractEngine _engine;

        // Coordenadas relativas al frame. La zona se muestra en el visor para orientar la cámara.
        public static OpenCvSharp.Rect GetRecognitionRegion(int width, int height) =>
            new((int)(width * .10), (int)(height * .30), (int)(width * .80), (int)(height * .60));

        public PlateReaderService()
        {
            _tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Model", "rfdetr_alpr.onnx");

            // Inicialización del detector YOLO/RFDETR
            _detector = new RfdetrPlateDetector(modelPath);
            _engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
            _engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
        }

        // IMPLEMENTACIÓN DE LA INTERFAZ
        public async Task<string> RecognizePlateAsync(byte[] imageFrame)
        {
            if (imageFrame == null || imageFrame.Length == 0) return string.Empty;

            var result = await DetectPlateWithRegionsAsync(imageFrame);
            return result.PlateNumber;
        }

        public async Task<PlateDetectionResult> DetectPlateWithRegionsAsync(byte[] imageFrame)
        {
            return await Task.Run(() =>
            {
                var result = new PlateDetectionResult();

                try
                {
                    if (imageFrame == null || imageFrame.Length == 0) return result;

                    using var src = Cv2.ImDecode(imageFrame, ImreadModes.Color);
                    if (src.Empty()) return result;

                    var roi = GetRecognitionRegion(src.Width, src.Height);
                    using var searchArea = new Mat(src, roi);
                    var regions = _detector.Detect(searchArea);

                    // 2. Si no detectó nada, fallback
                    if (regions.Count == 0)
                        regions = DetectPlateRegions(searchArea);

                    // El detector trabaja en la ROI; los rectángulos se trasladan al frame original.
                    regions = regions.Select(r => new OpenCvSharp.Rect(r.X + roi.X, r.Y + roi.Y, r.Width, r.Height))
                        .Select(r => OpenCvSharp.Rect.Intersect(r, new OpenCvSharp.Rect(0, 0, src.Width, src.Height)))
                        .Where(r => r.Width >= 60 && r.Height >= 20)
                        .Take(3).ToList();
                    result.DetectedRegions = regions;

                    foreach (var rect in regions)
                    {
                        var padded = OpenCvSharp.Rect.Intersect(
                            new OpenCvSharp.Rect(rect.X - rect.Width / 20, rect.Y - rect.Height / 8,
                                rect.Width + rect.Width / 10, rect.Height + rect.Height / 4),
                            new OpenCvSharp.Rect(0, 0, src.Width, src.Height));
                        using var plate = new Mat(src, padded);
                        var readings = new List<string>();

                        lock (_engine)
                        {
                            // El encabezado (por ejemplo, ECUADOR) está encima del número.
                            // El detector y la captura conservan la placa completa, pero el
                            // OCR comienza en la franja de caracteres para evitar esa línea.
                            foreach (double topFraction in new[] { .22, .12, 0.0 })
                            {
                                int top = (int)(plate.Height * topFraction);
                                using var characterBand = new Mat(plate,
                                    new OpenCvSharp.Rect(0, top, plate.Width, plate.Height - top));
                                foreach (var candidate in CreateOcrCandidates(characterBand))
                                    readings.AddRange(ReadPlateFromCandidate(_engine, candidate));

                                // Varios filtros deben coincidir antes de aceptar la lectura.
                                // Solo ampliamos la zona si el recorte aún no es concluyente.
                                if (readings.GroupBy(text => text).Any(group => group.Count() >= 3))
                                    break;
                            }
                        }

                        if (readings.Count > 0)
                        {
                            result.PlateNumber = readings.GroupBy(text => text)
                                .OrderByDescending(group => group.Count())
                                .First().Key;
                            result.PlateImage = plate.ToBytes(".jpg");
                            return result;
                        }
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en detección: {ex.Message}");
                    return result;
                }
            });
        }
        private static List<OpenCvSharp.Rect> DetectPlateRegions(Mat src)
        {
            var regions = new List<OpenCvSharp.Rect>();
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            using var normalized = new Mat();
            using (var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8))) clahe.Apply(gray, normalized);
            using var blurred = new Mat(); Cv2.GaussianBlur(normalized, blurred, new Size(5, 5), 0);
            using var edges = new Mat(); Cv2.Canny(blurred, edges, 100, 200);
            Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            foreach (var contour in contours)
            {
                var rect = Cv2.BoundingRect(contour);
                float aspectRatio = (float)rect.Width / rect.Height;
                if (aspectRatio > 2.2 && aspectRatio < 5.8 && rect.Width > 80)
                    regions.Add(rect);
            }
            return regions.OrderByDescending(r => r.Width * r.Height).ToList();
        }

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

        private static void AddCandidate(ICollection<byte[]> candidates, Mat image)
        {
            if (!image.Empty()) candidates.Add(image.ToBytes(".png"));
        }

        private static IEnumerable<string> ReadPlateFromCandidate(TesseractEngine engine, byte[] candidateImage)
        {
            foreach (PageSegMode mode in new[] { PageSegMode.SingleWord, PageSegMode.SingleLine })
            {
                engine.DefaultPageSegMode = mode;
                using var image = Pix.LoadFromMemory(candidateImage);
                using var page = engine.Process(image);
                string text = ExtractPlate(page.GetText());
                if (!string.IsNullOrWhiteSpace(text)) yield return text;
            }
        }

        private static string ExtractPlate(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
            string compact = Regex.Replace(rawText.ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
            var match = PlateRegex.Match(compact);
            if (!match.Success) return string.Empty;
            string plate = match.Value;
            return plate.Insert(3, "-");
        }

        public Task<string> DetectPlateAsync(byte[] image)
        {
            return RecognizePlateAsync(image);
        }

        public Task<string> RecognizePlateAsync()
        {
            throw new NotImplementedException();
        }

        public void Dispose() => _engine.Dispose();
    }
}
