// Basic DataIngestion Example
// Demonstrates PdfPig data ingestion with and without layout analysis and page images.
using UglyToad.PdfPig.DataIngestion;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

var pdfPath = args.Length > 0 ? args[0] : null;

if (pdfPath is null || !File.Exists(pdfPath))
{
    Console.WriteLine("Usage: dotnet run -- <pdf-path>");
    return;
}

// Example 1: Basic reading (flat text, no layout analysis, no page images)
Console.WriteLine("=== Basic Reading (renderPageImages: false) ===");
var reader = new PdfPigReader(renderPageImages: false);
using var stream = File.OpenRead(pdfPath);
var document = await reader.ReadAsync(stream, pdfPath, "application/pdf");
Console.WriteLine($"Sections: {document.Sections.Count}");
foreach (var section in document.Sections)
{
    Console.WriteLine($"  Page {section.PageNumber}: {section.Elements.Count} elements");
    var hasImage = section.HasMetadata && section.Metadata.ContainsKey("page_image");
    Console.WriteLine($"    Page image: {(hasImage ? "yes" : "no")}");
    foreach (var element in section.Elements)
    {
        var preview = element.Text is { Length: > 0 }
            ? element.Text[..Math.Min(80, element.Text.Length)]
            : "(empty)";
        Console.WriteLine($"    [{element.GetType().Name}] {preview}");
    }
}

// Example 2: With HeuristicPageSegmenter + page images
Console.WriteLine("\n=== Heuristic Layout (renderPageImages: true) ===");
var structuredReader = new PdfPigReader(
    segmenter: HeuristicPageSegmenter.Instance,
    renderPageImages: true);
using var stream2 = File.OpenRead(pdfPath);
var doc2 = await structuredReader.ReadAsync(stream2, pdfPath, "application/pdf");
Console.WriteLine($"Sections: {doc2.Sections.Count}");
foreach (var section in doc2.Sections)
{
    Console.WriteLine($"  Page {section.PageNumber}: {section.Elements.Count} elements");

    // Show page image metadata
    if (section.HasMetadata &&
        section.Metadata.TryGetValue("page_image", out var imgObj) &&
        imgObj is byte[] imgBytes)
    {
        Console.WriteLine($"    Page image: {imgBytes.Length:N0} bytes (PNG)");
    }

    // Show page dimensions
    if (section.HasMetadata &&
        section.Metadata.TryGetValue("page_width", out var w) &&
        section.Metadata.TryGetValue("page_height", out var h))
    {
        Console.WriteLine($"    Page size: {w:F1} × {h:F1} pt");
    }

    foreach (var element in section.Elements)
    {
        var preview = element.Text is { Length: > 0 }
            ? element.Text[..Math.Min(80, element.Text.Length)]
            : "(empty)";

        // Show bounding box metadata
        var bbox = "";
        if (element.HasMetadata &&
            element.Metadata.TryGetValue("BoundingBox.Left", out var left) &&
            element.Metadata.TryGetValue("BoundingBox.Bottom", out var bottom) &&
            element.Metadata.TryGetValue("BoundingBox.Right", out var right) &&
            element.Metadata.TryGetValue("BoundingBox.Top", out var top))
        {
            bbox = $" bbox=({left:F1},{bottom:F1})-({right:F1},{top:F1})";
        }

        Console.WriteLine($"    [{element.GetType().Name}]{bbox} {preview}");
    }
}
