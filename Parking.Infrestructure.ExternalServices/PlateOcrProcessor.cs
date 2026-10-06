using OpenCvSharp;
using System.Text.RegularExpressions;
using Tesseract;

namespace Parking.Infrastructure.ExternalServices;

/// <summary>Prepara recortes de placa y obtiene lecturas válidas mediante Tesseract.</summary>
internal sealed class PlateOcrProcessor : IDisposable
{
    private static readonly Regex PlateRegex = new(@"[A-Z]{3}\d{3,4}", RegexOptions.Compiled);
    private readonly Lazy<TesseractEngine> _engine;

    /// <summary>Difiere la carga de OCR hasta que se recibe la primera imagen.</summary>
    public PlateOcrProcessor(string tessDataPath)
    {
        _engine = new Lazy<TesseractEngine>(() =>
        {
            var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
            return engine;
        });
    }

    /// <summary>Prueba tres franjas de la placa y detiene la búsqueda al obtener consenso.</summary>
    public List<string> ReadRegion(Mat plate)
    {
        var readings = new List<string>();
        var engine = _engine.Value;
        lock (engine)
        {
            foreach (double topFraction in new[] { .22, .12, 0.0 })
            {
                int top = (int)(plate.Height * topFraction);
                using var band = new Mat(plate, new OpenCvSharp.Rect(0, top, plate.Width, plate.Height - top));
                foreach (var candidate in CreateOcrCandidates(band))
                    readings.AddRange(ReadPlateFromCandidate(engine, candidate));
                if (readings.GroupBy(text => text).Any(group => group.Count() >= 3))
                    break;
            }
        }
        return readings;
    }

    /// <summary>Crea variantes de escala y contraste para leer caracteres difíciles.</summary>
    private static List<byte[]> CreateOcrCandidates(Mat src)
    {
        var candidates = new List<byte[]>();
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // Reducir una foto de pantalla atenúa reflejos y patrones de píxeles.
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

        using var adaptive = new Mat();
        Cv2.AdaptiveThreshold(contrast, adaptive, 255, AdaptiveThresholdTypes.GaussianC,
            ThresholdTypes.Binary, 31, 11);
        AddCandidate(candidates, adaptive);

        // La segunda escala atenúa patrones de pantalla y confirma caracteres ambiguos.
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

    /// <summary>Agrega una variante PNG si la transformación generó una imagen utilizable.</summary>
    private static void AddCandidate(ICollection<byte[]> candidates, Mat image)
    {
        if (!image.Empty()) candidates.Add(image.ToBytes(".png"));
    }

    /// <summary>Lee cada variante como palabra y como línea.</summary>
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

    /// <summary>Extrae el patrón de placa y coloca el guion después de las tres letras.</summary>
    private static string ExtractPlate(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        string compact = Regex.Replace(rawText.ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
        var match = PlateRegex.Match(compact);
        return match.Success ? match.Value.Insert(3, "-") : string.Empty;
    }

    /// <summary>Libera Tesseract solo si llegó a inicializarse.</summary>
    public void Dispose()
    {
        if (_engine.IsValueCreated) _engine.Value.Dispose();
    }
}
