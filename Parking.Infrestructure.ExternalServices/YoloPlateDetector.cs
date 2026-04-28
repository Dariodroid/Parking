using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class YoloPlateDetector
{
    protected readonly InferenceSession _session;   // ← protected para que la clase hija pueda usarlo

    public YoloPlateDetector(string modelPath)
    {
        _session = new InferenceSession(modelPath);
    }

    // 🔥 Ahora es VIRTUAL para que override funcione
    public virtual List<OpenCvSharp.Rect> Detect(Mat image)
    {
        var resized = image.Resize(new Size(640, 640));
        var tensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });

        for (int y = 0; y < 640; y++)
        {
            for (int x = 0; x < 640; x++)
            {
                var pixel = resized.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = pixel.Item2 / 255f; // R
                tensor[0, 1, y, x] = pixel.Item1 / 255f; // G
                tensor[0, 2, y, x] = pixel.Item0 / 255f; // B
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", tensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        var boxes = new List<OpenCvSharp.Rect>();
        for (int i = 0; i < output.Dimensions[1]; i++)
        {
            float score = output[0, i, 4];
            if (score < 0.5f) continue;

            float x = output[0, i, 0] * image.Width;
            float y = output[0, i, 1] * image.Height;
            float w = output[0, i, 2] * image.Width;
            float h = output[0, i, 3] * image.Height;

            boxes.Add(new OpenCvSharp.Rect(
                (int)(x - w / 2),
                (int)(y - h / 2),
                (int)w,
                (int)h
            ));
        }
        return boxes;
    }
}