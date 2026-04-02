// Example: ONNX Layout Detection
// NOTE: Requires an ONNX model file. Download RT-DETR Heron from HuggingFace.
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx.Models;

Console.WriteLine("=== ONNX Layout Detection Example ===");

var modelPath = args.Length > 0 ? args[0] : "heron_v2.onnx";
var pdfPath = args.Length > 1 ? args[1] : "sample.pdf";

if (!File.Exists(modelPath))
{
    Console.WriteLine($"Model not found: {modelPath}");
    Console.WriteLine("Download from: https://huggingface.co/ds4sd/docling-models");
    return;
}

// Example 1: RT-DETR Layout Model
using var model = new RtDetrLayoutModel(modelPath);
using var segmenter = new OnnxPageSegmenter(model, new OnnxSegmenterOptions { ConfidenceThreshold = 0.3f });

using var document = PdfDocument.Open(pdfPath);
foreach (var page in document.GetPages())
{
    var words = page.GetWords();
    var blocks = segmenter.GetBlocks(words);
    Console.WriteLine($"Page {page.Number}: {blocks.Count} blocks detected");
    foreach (var block in blocks)
    {
        Console.WriteLine($"  [{block.BoundingBox}] {block.Text[..Math.Min(60, block.Text.Length)]}...");
    }
}
