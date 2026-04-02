// Enricher Composition Example
// Demonstrates composing multiple PdfPig vision processors with a real Ollama backend.
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
    Console.WriteLine($"  Requires Ollama running at {ollamaEndpoint}");
    Console.WriteLine("  Set OLLAMA_ENDPOINT env var to override.");
    Console.WriteLine($"  Default model: {modelName}");
    return;
}

// Step 1: Read PDF with heuristic layout + page image rendering
Console.WriteLine($"Reading PDF: {pdfPath}");
var reader = new PdfPigReader(
    segmenter: HeuristicPageSegmenter.Instance,
    renderPageImages: true);

using var stream = File.OpenRead(pdfPath);
var document = await reader.ReadAsync(stream, pdfPath, "application/pdf");

Console.WriteLine($"  Pages: {document.Sections.Count}");
Console.WriteLine($"  Elements: {document.Sections.Sum(s => s.Elements.Count)}");

foreach (var section in document.Sections)
{
    if (section.HasMetadata &&
        section.Metadata.TryGetValue("page_image", out var img) &&
        img is byte[] imgBytes)
    {
        Console.WriteLine($"  Page {section.PageNumber}: image rendered ({imgBytes.Length:N0} bytes)");
    }
}

// Step 2: Apply enrichers via Ollama
try
{
    using var httpClient = new HttpClient
    {
        BaseAddress = new Uri(ollamaEndpoint),
        Timeout = TimeSpan.FromMinutes(5)
    };
    IChatClient chatClient = new OllamaSharp.OllamaApiClient(httpClient, modelName);

    // --- Pattern 1: VisionOcrEnricher ---
    Console.WriteLine($"\nApplying VisionOcrEnricher (model: {modelName})...");
    var ocrProcessor = new VisionOcrEnricher(chatClient);
    document = await ocrProcessor.ProcessAsync(document);

    var ocrCount = document.EnumerateContent()
        .Count(e => e.HasMetadata && e.Metadata.ContainsKey("ocr_source"));
    Console.WriteLine($"  Elements enriched via OCR: {ocrCount}");

    // --- Pattern 2: VisionTableEnricher ---
    Console.WriteLine("Applying VisionTableEnricher...");
    var tableProcessor = new VisionTableEnricher(chatClient);
    document = await tableProcessor.ProcessAsync(document);

    var tableCount = document.EnumerateContent()
        .Count(e => e.HasMetadata && e.Metadata.ContainsKey("enriched_markdown_table"));
    Console.WriteLine($"  Tables enriched with markdown: {tableCount}");

    // --- Pattern 3: ContextualChunkEnricher (operates on chunks) ---
    Console.WriteLine("Applying ContextualChunkEnricher on text chunks...");
    var chunkEnricher = new ContextualChunkEnricher(chatClient);

    // Create chunks from document elements
    var chunks = document.EnumerateContent()
        .Where(e => !string.IsNullOrWhiteSpace(e.Text))
        .Select(e => new IngestionChunk<string>(e.Text!, document, $"Page {e.PageNumber}"));

    var enrichedChunks = new List<IngestionChunk<string>>();
    await foreach (var chunk in chunkEnricher.ProcessAsync(ToAsync(chunks)))
    {
        enrichedChunks.Add(chunk);
    }
    Console.WriteLine($"  Chunks enriched with summaries: {enrichedChunks.Count}");

    // --- Print enriched results ---
    Console.WriteLine("\n=== Enriched Document ===");
    foreach (var section in document.Sections)
    {
        Console.WriteLine($"\nPage {section.PageNumber}:");
        foreach (var element in section.Elements)
        {
            var preview = element.Text is { Length: > 0 }
                ? element.Text[..Math.Min(100, element.Text.Length)]
                : "(empty)";
            var tags = new List<string>();
            if (element.HasMetadata && element.Metadata.ContainsKey("ocr_source"))
                tags.Add("OCR");
            if (element.HasMetadata && element.Metadata.ContainsKey("enriched_markdown_table"))
                tags.Add("Table→MD");
            var tagStr = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";
            Console.WriteLine($"  [{element.GetType().Name}]{tagStr} {preview}");
        }
    }

    if (enrichedChunks.Count > 0)
    {
        Console.WriteLine("\n=== Chunk Summaries (first 5) ===");
        foreach (var chunk in enrichedChunks.Take(5))
        {
            var contentPreview = chunk.Content[..Math.Min(60, chunk.Content.Length)];
            var summary = chunk.Metadata.TryGetValue("contextual_summary", out var s) ? s : "(none)";
            Console.WriteLine($"  [{chunk.Context}] \"{contentPreview}...\"");
            Console.WriteLine($"    Summary: {summary}");
        }
    }
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"\nCould not connect to Ollama at {ollamaEndpoint}");
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine("Make sure Ollama is running: ollama serve");
    Console.WriteLine($"And pull a vision model: ollama pull {modelName}");
}

static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> source)
{
    foreach (var item in source)
    {
        yield return item;
    }
    await Task.CompletedTask;
}
