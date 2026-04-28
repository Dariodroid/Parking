using OpenCvSharp;
using Parking.Application.EntityService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace Parking.Infrastructure.ExternalServices
{
    public class PlateDetectionResult
    {
        public string PlateNumber { get; set; } = string.Empty;
        public List<OpenCvSharp.Rect> DetectedRegions { get; set; } = new List<OpenCvSharp.Rect>();
        public bool HasDetection => DetectedRegions.Count > 0;
    }

    public class PlateReaderService : IPlateService
    {
        private static readonly Regex PlateRegex = new(@"[A-Z]{3}\d{3,4}|[A-Z]{2}\d{4}", RegexOptions.Compiled);
        private readonly string _tessDataPath;
        // Nota: Asegúrate de tener implementada la clase RfdetrPlateDetector o cámbiala por una genérica
        private readonly YoloPlateDetector _detector;

        public PlateReaderService()
        {
            _tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Model", "rfdetr_alpr.onnx");

            // Inicialización del detector YOLO/RFDETR
            _detector = new RfdetrPlateDetector(modelPath);
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

                    using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
                    engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

                    // 1. Detector principal
                    var regions = _detector.Detect(src);

                    // 2. Si no detectó nada, fallback
                    if (regions.Count == 0)
                        regions = DetectPlateRegions(src);

                    // 3. Forzamos solo UNA caja (la más grande)
                    if (regions.Count > 1)
                    {
                        regions = regions
                            .OrderByDescending(r => r.Width * r.Height)
                            .Take(1)
                            .ToList();
                    }

                    result.DetectedRegions = regions;

                    foreach (var rect in regions)
                    {
                        using var plate = new Mat(src, rect);
                        var candidateImages = CreateOcrCandidates(plate);

                        foreach (var candidate in candidateImages)
                        {
                            string text = ReadPlateFromCandidate(engine, candidate);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                result.PlateNumber = text;
                                return result;
                            }
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
            using var blurred = new Mat(); Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
            using var edges = new Mat(); Cv2.Canny(blurred, edges, 100, 200);
            Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            foreach (var contour in contours)
            {
                var rect = Cv2.BoundingRect(contour);
                float aspectRatio = (float)rect.Width / rect.Height;
                if (aspectRatio > 2.2 && aspectRatio < 5.8 && rect.Width > 80)
                    regions.Add(rect);
            }
            return regions;
        }

        private static List<byte[]> CreateOcrCandidates(Mat src)
        {
            var candidates = new List<byte[]>();
            using var gray = new Mat(); Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            using var enlarged = new Mat(); Cv2.Resize(gray, enlarged, new Size(), 2.0, 2.0, InterpolationFlags.Cubic);
            AddCandidate(candidates, enlarged);

            // Filtros para mejorar legibilidad
            using var adaptive = new Mat(); Cv2.AdaptiveThreshold(enlarged, adaptive, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 31, 11);
            AddCandidate(candidates, adaptive);

            return candidates;
        }

        private static void AddCandidate(ICollection<byte[]> candidates, Mat image)
        {
            if (!image.Empty()) candidates.Add(image.ToBytes(".png"));
        }

        private static string ReadPlateFromCandidate(TesseractEngine engine, byte[] candidateImage)
        {
            foreach (PageSegMode mode in new[] { PageSegMode.SingleLine, PageSegMode.SingleWord })
            {
                engine.DefaultPageSegMode = mode;
                using var image = Pix.LoadFromMemory(candidateImage);
                using var page = engine.Process(image);
                string text = ExtractPlate(page.GetText());
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            return string.Empty;
        }

        private static string ExtractPlate(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
            string compact = Regex.Replace(rawText.ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
            var match = PlateRegex.Match(compact);
            if (!match.Success) return string.Empty;
            string plate = match.Value;
            return plate.Length > 3 ? plate.Insert(3, "-") : plate;
        }

        public Task<string> DetectPlateAsync(byte[] image)
        {
            throw new NotImplementedException();
        }

        public Task<string> RecognizePlateAsync()
        {
            throw new NotImplementedException();
        }
    }
}