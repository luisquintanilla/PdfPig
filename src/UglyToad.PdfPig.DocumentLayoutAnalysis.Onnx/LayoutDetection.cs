namespace UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx
{
    using UglyToad.PdfPig.Core;

    /// <summary>
    /// A single detected layout element from an ONNX model.
    /// </summary>
    public record LayoutDetection(
        PdfRectangle BoundingBox,
        string Label,
        int ClassId,
        float Confidence);
}
