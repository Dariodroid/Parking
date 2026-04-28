using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class RfdetrPlateDetector : YoloPlateDetector
{
    public RfdetrPlateDetector(string modelPath) : base(modelPath)
    {
        Console.WriteLine("=== RF-DETR DETECTOR CREADO CORRECTAMENTE ===");
        Console.WriteLine($"Modelo: {modelPath}");
        Console.WriteLine($"Existe: {File.Exists(modelPath)}");
    }

    public override List<OpenCvSharp.Rect> Detect(Mat image)
    {
        if (image == null || image.Empty())
            return new List<OpenCvSharp.Rect>();

        int inputSize = 576;
        var resized = image.Resize(new Size(inputSize, inputSize));

        var tensor = new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize });
        for (int y = 0; y < inputSize; y++)
        {
            for (int x = 0; x < inputSize; x++)
            {
                var pixel = resized.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = pixel.Item2 / 255f;
                tensor[0, 1, y, x] = pixel.Item1 / 255f;
                tensor[0, 2, y, x] = pixel.Item0 / 255f;
            }
        }

        var inputName = _session.InputMetadata.Keys.First();
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        using var results = _session.Run(inputs);
        var outputs = results.ToList();

        if (outputs.Count < 2)
            return new List<OpenCvSharp.Rect>();

        var boxesTensor = outputs[0].AsTensor<float>();
        var scoresTensor = outputs[1].AsTensor<float>();

        var boxShape = boxesTensor.Dimensions.ToArray();
        int numQueries = (boxShape.Length == 3 && boxShape[2] == 4) ? boxShape[1] : 0;

        OpenCvSharp.Rect? bestRect = null;
        float bestScore = -1f;

        for (int i = 0; i < numQueries; i++)
        {
            float score = 0f;
            try
            {
                score = scoresTensor.Dimensions.Length == 2 ? scoresTensor[0, i] : 0f;
            }
            catch { score = 0f; }

            // Umbral más equilibrado pero estricto
            if (score < 0.99f) continue;

            float x1 = boxesTensor[0, i, 0] * image.Width;
            float y1 = boxesTensor[0, i, 1] * image.Height;
            float x2 = boxesTensor[0, i, 2] * image.Width;
            float y2 = boxesTensor[0, i, 3] * image.Height;

            int w = (int)(x2 - x1);
            int h = (int)(y2 - y1);

            // Filtro de tamaño razonable para una placa de vehículo
            if (w < 70 || h < 30 || w > image.Width * 0.8) continue;

            var rect = new OpenCvSharp.Rect(Math.Max(0, (int)x1), Math.Max(0, (int)y1), w, h);

            // Nos quedamos solo con la mejor detección
            if (score > bestScore)
            {
                bestScore = score;
                bestRect = rect;
            }
        }

        if (bestRect == null)
        {
            Console.WriteLine("RF-DETR → No se detectó ninguna placa confiable");
            return new List<OpenCvSharp.Rect>();
        }

        Console.WriteLine($"✅ MEJOR PLACA DETECTADA → Score: {bestScore:F3} | Rect: {bestRect}");
        return new List<OpenCvSharp.Rect> { bestRect.Value };
    }
}