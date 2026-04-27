namespace UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx
{
    using Microsoft.ML.OnnxRuntime;

    /// <summary>
    /// Options for configuring the <see cref="OnnxPageSegmenter"/>.
    /// </summary>
    public record OnnxSegmenterOptions
    {
        /// <summary>
        /// Minimum confidence threshold for detections (0.0 to 1.0).
        /// </summary>
        public float ConfidenceThreshold { get; init; } = 0.3f;

        /// <summary>
        /// ONNX Runtime session options. Use to configure GPU, thread count, etc.
        /// </summary>
        public SessionOptions? SessionOptions { get; init; }

        /// <summary>
        /// DPI for rendering the page image. Higher values improve accuracy but are slower.
        /// </summary>
        public int RenderDpi { get; init; } = 150;
    }
}
