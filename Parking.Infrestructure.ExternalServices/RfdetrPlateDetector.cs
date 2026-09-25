using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

public class RfdetrPlateDetector : YoloPlateDetector
{
    public RfdetrPlateDetector(string modelPath) : base(modelPath)
    {
    }

    public override List<OpenCvSharp.Rect> Detect(Mat image)
    {
        if (image == null || image.Empty())
            return new List<OpenCvSharp.Rect>();

        int inputSize = 576;
        using var resized = image.Resize(new Size(inputSize, inputSize));

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

        // Este export de RF-DETR entrega cinco pares (logits, cajas cx/cy/ancho/alto).
        // El último par corresponde a la predicción final. Los nombres iniciales
        // "boxes" y "scores" están invertidos respecto a su contenido real.
        if (outputs.Count < 2)
            return new List<OpenCvSharp.Rect>();

        var scoresTensor = outputs[^2].AsTensor<float>();
        var boxesTensor = outputs[^1].AsTensor<float>();
        if (scoresTensor.Dimensions.Length != 3 || scoresTensor.Dimensions[2] != 1 ||
            boxesTensor.Dimensions.Length != 3 || boxesTensor.Dimensions[2] != 4 ||
            scoresTensor.Dimensions[1] != boxesTensor.Dimensions[1])
            return new List<OpenCvSharp.Rect>();

        int numQueries = boxesTensor.Dimensions[1];
        var candidates = new List<(OpenCvSharp.Rect Rect, float Score)>();

        for (int i = 0; i < numQueries; i++)
        {
            float logit = scoresTensor[0, i, 0];
            // Los logits del modelo no son probabilidades. Cero equivale al 50%.
            if (logit < 0f) continue;

            float cx = boxesTensor[0, i, 0] * image.Width;
            float cy = boxesTensor[0, i, 1] * image.Height;
            float width = boxesTensor[0, i, 2] * image.Width;
            float height = boxesTensor[0, i, 3] * image.Height;
            int left = Math.Clamp((int)(cx - width / 2), 0, image.Width);
            int top = Math.Clamp((int)(cy - height / 2), 0, image.Height);
            int right = Math.Clamp((int)(cx + width / 2), 0, image.Width);
            int bottom = Math.Clamp((int)(cy + height / 2), 0, image.Height);
            if (right <= left || bottom <= top) continue;
            var rect = new OpenCvSharp.Rect(left, top, right - left, bottom - top);
            if (rect.Width < 40 || rect.Height < 14 || rect.Width > image.Width * .9)
                continue;
            candidates.Add((rect, logit));
        }

        return candidates.OrderByDescending(c => c.Score).Take(3).Select(c => c.Rect).ToList();
    }
}
