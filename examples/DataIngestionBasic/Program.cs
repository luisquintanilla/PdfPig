// Basic DataIngestion Example
using UglyToad.PdfPig.DataIngestion;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

var pdfPath = args.Length > 0 ? args[0] : "sample.pdf";

// Example 1: Basic reading (flat text, no layout analysis)
Console.WriteLine("=== Basic Reading ===");
var reader = new PdfPigReader();
using var stream = File.OpenRead(pdfPath);
var document = await reader.ReadAsync(stream, pdfPath, "application/pdf");
Console.WriteLine($"Sections: {document.Sections.Count}");
foreach (var section in document.Sections)
{
    Console.WriteLine($"  Section: {section.Elements.Count} elements");
    foreach (var element in section.Elements)
    {
        var preview = element.Text is { Length: > 0 }
            ? element.Text[..Math.Min(80, element.Text.Length)]
            : "(empty)";
        Console.WriteLine($"    [{element.GetType().Name}] {preview}");
    }
}

// Example 2: With HeuristicPageSegmenter
Console.WriteLine("\n=== With Heuristic Layout ===");
var structuredReader = new PdfPigReader(segmenter: HeuristicPageSegmenter.Instance);
using var stream2 = File.OpenRead(pdfPath);
var doc2 = await structuredReader.ReadAsync(stream2, pdfPath, "application/pdf");
Console.WriteLine($"Sections: {doc2.Sections.Count}");
foreach (var section in doc2.Sections)
{
    Console.WriteLine($"  Section: {section.Elements.Count} elements");
    foreach (var element in section.Elements)
    {
        var preview = element.Text is { Length: > 0 }
            ? element.Text[..Math.Min(80, element.Text.Length)]
            : "(empty)";
        Console.WriteLine($"    [{element.GetType().Name}] {preview}");
    }
}
