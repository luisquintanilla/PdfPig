// Full RAG Pipeline Example
// Demonstrates progressive levels of document understanding with real code.
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using UglyToad.PdfPig.DataIngestion;
using UglyToad.PdfPig.DataIngestion.Processors;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

var pdfPath = args.Length > 0 ? args[0] : null;
var modelName = args.Length > 1 ? args[1] : "llama3.2-vision";
var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://localhost:11434";

if (pdfPath is null || !File.Exists(pdfPath))
{
    Console.WriteLine("Usage: dotnet run -- <pdf-path> [model-name]");
    Console.WriteLine($"  Levels 1-4 run without Ollama. Level 5 requires Ollama at {ollamaEndpoint}.");
    return;
}

// ─── Level 1: Basic Text Extraction ───
Console.WriteLine("=== Level 1: Basic Text Extraction ===");
Console.WriteLine("  PdfPigReader() — flat text, no layout analysis");
var basicReader = new PdfPigReader();
using (var stream1 = File.OpenRead(pdfPath))
{
    var doc1 = await basicReader.ReadAsync(stream1, pdfPath, "application/pdf");
    Console.WriteLine($"  Pages: {doc1.Sections.Count}");
    var totalElements1 = doc1.Sections.Sum(s => s.Elements.Count);
    Console.WriteLine($"  Elements: {totalElements1}");
    foreach (var section in doc1.Sections)
    {
        Console.WriteLine($"  Page {section.PageNumber}: {section.Elements.Count} element(s)");
        foreach (var el in section.Elements.Take(3))
        {
            var preview = el.Text is { Length: > 0 }
                ? el.Text[..Math.Min(80, el.Text.Length)]
                : "(empty)";
            Console.WriteLine($"    [{el.GetType().Name}] {preview}");
        }
        if (section.Elements.Count > 3)
            Console.WriteLine($"    ... and {section.Elements.Count - 3} more");
    }
}

// ─── Level 2: Heuristic Layout ───
Console.WriteLine("\n=== Level 2: Heuristic Layout ===");
Console.WriteLine("  PdfPigReader(segmenter: HeuristicPageSegmenter.Instance) — structural blocks");
var heuristicReader = new PdfPigReader(
    segmenter: HeuristicPageSegmenter.Instance);
using (var stream2 = File.OpenRead(pdfPath))
{
    var doc2 = await heuristicReader.ReadAsync(stream2, pdfPath, "application/pdf");
    var totalElements2 = doc2.Sections.Sum(s => s.Elements.Count);
    Console.WriteLine($"  Pages: {doc2.Sections.Count}, Elements: {totalElements2}");
    foreach (var section in doc2.Sections)
    {
        Console.WriteLine($"  Page {section.PageNumber}: {section.Elements.Count} element(s)");
        foreach (var el in section.Elements.Take(3))
        {
            var preview = el.Text is { Length: > 0 }
                ? el.Text[..Math.Min(80, el.Text.Length)]
                : "(empty)";
            var bbox = "";
            if (el.HasMetadata &&
                el.Metadata.TryGetValue("BoundingBox.Left", out var left) &&
                el.Metadata.TryGetValue("BoundingBox.Top", out var top))
            {
                bbox = $" @({left:F0},{top:F0})";
            }
            Console.WriteLine($"    [{el.GetType().Name}]{bbox} {preview}");
        }
        if (section.Elements.Count > 3)
            Console.WriteLine($"    ... and {section.Elements.Count - 3} more");
    }
}

// ─── Level 3: Hybrid Mode (Text + Images) ───
Console.WriteLine("\n=== Level 3: Hybrid Mode ===");
Console.WriteLine("  PdfPigReader(segmenter: ..., mode: PdfReadingMode.Hybrid) — text + images for vision models");
var imageReader = new PdfPigReader(
    segmenter: HeuristicPageSegmenter.Instance,
    mode: PdfReadingMode.Hybrid);
IngestionDocument doc3;
using (var stream3 = File.OpenRead(pdfPath))
{
    doc3 = await imageReader.ReadAsync(stream3, pdfPath, "application/pdf");
}
var totalElements3 = doc3.Sections.Sum(s => s.Elements.Count);
Console.WriteLine($"  Pages: {doc3.Sections.Count}, Elements: {totalElements3}");
foreach (var section in doc3.Sections)
{
    var imageInfo = "(no image)";
    if (section.HasMetadata &&
        section.Metadata.TryGetValue("page_image", out var imgObj) &&
        imgObj is byte[] imgBytes)
    {
        imageInfo = $"image: {imgBytes.Length:N0} bytes";
    }
    var dims = "";
    if (section.HasMetadata &&
        section.Metadata.TryGetValue("page_width", out var w) &&
        section.Metadata.TryGetValue("page_height", out var h))
    {
        dims = $", page: {w:F0}×{h:F0} pt";
    }
    Console.WriteLine($"  Page {section.PageNumber}: {section.Elements.Count} element(s), {imageInfo}{dims}");
}

// ─── Level 4: VisionOnly Mode ───
Console.WriteLine("\n=== Level 4: VisionOnly Mode ===");
Console.WriteLine("  PdfPigReader(mode: PdfReadingMode.VisionOnly) — all pages sent to VLM, no text extraction");
var visionReader = new PdfPigReader(mode: PdfReadingMode.VisionOnly);
using (var streamV = File.OpenRead(pdfPath))
{
    var docV = await visionReader.ReadAsync(streamV, pdfPath, "application/pdf");
    Console.WriteLine($"  Pages: {docV.Sections.Count}");
    foreach (var section in docV.Sections)
    {
        var imageInfo = "(no image)";
        if (section.HasMetadata &&
            section.Metadata.TryGetValue("page_image", out var vImgObj) &&
            vImgObj is byte[] vImgBytes)
        {
            imageInfo = $"image: {vImgBytes.Length:N0} bytes";
        }
        Console.WriteLine($"  Page {section.PageNumber}: {section.Elements.Count} element(s), {imageInfo}");
    }
}

// ─── Level 5: Vision Enrichment (requires Ollama) ───
Console.WriteLine("\n=== Level 5: Vision Enrichment (Ollama) ===");
try
{
    using var httpClient = new HttpClient
    {
        BaseAddress = new Uri(ollamaEndpoint),
        Timeout = TimeSpan.FromMinutes(5)
    };
    IChatClient chatClient = new OllamaSharp.OllamaApiClient(httpClient, modelName);
    Console.WriteLine($"  Using model: {modelName} at {ollamaEndpoint}");

    // Apply VisionOcrEnricher
    Console.WriteLine("  Applying VisionOcrEnricher...");
    doc3 = await new VisionOcrEnricher(chatClient).ProcessAsync(doc3);

    // Apply VisionTableEnricher
    Console.WriteLine("  Applying VisionTableEnricher...");
    doc3 = await new VisionTableEnricher(chatClient).ProcessAsync(doc3);

    // Apply ContextualChunkEnricher on text chunks
    Console.WriteLine("  Applying ContextualChunkEnricher...");
    var chunkEnricher = new ContextualChunkEnricher(chatClient);
    var chunks = doc3.EnumerateContent()
        .Where(e => !string.IsNullOrWhiteSpace(e.Text))
        .Select(e => new IngestionChunk<string>(e.Text!, doc3, $"Page {e.PageNumber}"));

    var enrichedChunks = new List<IngestionChunk<string>>();
    await foreach (var chunk in chunkEnricher.ProcessAsync(ToAsync(chunks)))
    {
        enrichedChunks.Add(chunk);
    }

    // Print enriched results
    Console.WriteLine($"\n  Enrichment complete:");
    var ocrCount = doc3.EnumerateContent()
        .Count(e => e.HasMetadata && e.Metadata.ContainsKey("ocr_source"));
    var tableCount = doc3.EnumerateContent()
        .Count(e => e.HasMetadata && e.Metadata.ContainsKey("enriched_markdown_table"));
    Console.WriteLine($"    OCR-enriched elements: {ocrCount}");
    Console.WriteLine($"    Table→markdown elements: {tableCount}");
    Console.WriteLine($"    Chunks with summaries: {enrichedChunks.Count}");

    foreach (var section in doc3.Sections)
    {
        Console.WriteLine($"\n  Page {section.PageNumber}:");
        foreach (var el in section.Elements)
        {
            var preview = el.Text is { Length: > 0 }
                ? el.Text[..Math.Min(80, el.Text.Length)]
                : "(empty)";
            var tags = new List<string>();
            if (el.HasMetadata && el.Metadata.ContainsKey("ocr_source"))
                tags.Add("OCR");
            if (el.HasMetadata && el.Metadata.ContainsKey("enriched_markdown_table"))
                tags.Add("Table→MD");
            var tagStr = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";
            Console.WriteLine($"    [{el.GetType().Name}]{tagStr} {preview}");
        }
    }

    if (enrichedChunks.Count > 0)
    {
        Console.WriteLine("\n  Chunk summaries (first 5):");
        foreach (var chunk in enrichedChunks.Take(5))
        {
            var contentPreview = chunk.Content[..Math.Min(60, chunk.Content.Length)];
            var summary = chunk.Metadata.TryGetValue("contextual_summary", out var s) ? s : "(none)";
            Console.WriteLine($"    [{chunk.Context}] \"{contentPreview}...\"");
            Console.WriteLine($"      Summary: {summary}");
        }
    }
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"  Could not connect to Ollama at {ollamaEndpoint}");
    Console.WriteLine($"  Error: {ex.Message}");
    Console.WriteLine("  Make sure Ollama is running: ollama serve");
    Console.WriteLine($"  And pull a vision model: ollama pull {modelName}");
    Console.WriteLine("  Levels 1-4 above ran successfully without Ollama.");
}

static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> source)
{
    foreach (var item in source)
    {
        yield return item;
    }
    await Task.CompletedTask;
}
