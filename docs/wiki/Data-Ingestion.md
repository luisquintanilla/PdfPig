# Data Ingestion #

PdfPig provides first-class integration with [Microsoft.Extensions.DataIngestion](https://learn.microsoft.com/dotnet/ai/conceptual/medi-overview) (MEDI) through the `UglyToad.PdfPig.DataIngestion` package. This makes it easy to incorporate PDF reading and enrichment into RAG (Retrieval-Augmented Generation) pipelines built on the MEDI framework.

## Prerequisites ##

Install the NuGet package:

    > Install-Package UglyToad.PdfPig.DataIngestion

For the vision and contextual enrichment processors you also need an `IChatClient` implementation from [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/conceptual/meai-overview), for example:

    > Install-Package Microsoft.Extensions.AI.OpenAI

## PdfPigReader ##

`PdfPigReader` is an `IngestionDocumentReader` that opens PDF files, segments pages using PdfPig's layout analysis, and produces an `IngestionDocument` with structured sections and elements.

### Constructor ###

```csharp
public PdfPigReader(IPageSegmenter? segmenter = null)
```

The optional `segmenter` parameter accepts any PdfPig page segmenter. If omitted it defaults to `DefaultPageSegmenter.Instance`. Other built-in options include:

+ `HeuristicPageSegmenter.Instance`
+ `RecursiveXYCut` (see [Document Layout Analysis](https://github.com/UglyToad/PdfPig/wiki/Document-Layout-Analysis))
+ ONNX-based segmenters

### ReadAsync ###

```csharp
public override async Task<IngestionDocument> ReadAsync(
    Stream source,
    string identifier,
    string mediaType,
    CancellationToken cancellationToken = default)
```

Returns an `IngestionDocument` containing:

+ One `IngestionDocumentSection` per page.
+ Each text block becomes an `IngestionDocumentParagraph` with bounding box metadata (`BoundingBox.Left`, `BoundingBox.Bottom`, `BoundingBox.Right`, `BoundingBox.Top`).

### Basic Example ###

    using UglyToad.PdfPig.DataIngestion;

    var reader = new PdfPigReader();
    using var stream = File.OpenRead("document.pdf");

    var doc = await reader.ReadAsync(stream, "document.pdf", "application/pdf");

    foreach (var section in doc.Sections)
    {
        foreach (var element in section.Elements)
        {
            Console.WriteLine(element.Text);
        }
    }

### With Layout Analysis ###

    using UglyToad.PdfPig.DataIngestion;
    using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

    var reader = new PdfPigReader(segmenter: HeuristicPageSegmenter.Instance);
    using var stream = File.OpenRead("document.pdf");

    var doc = await reader.ReadAsync(stream, "document.pdf", "application/pdf");

## Processors ##

The package includes three processors that leverage an `IChatClient` (from Microsoft.Extensions.AI) to enrich documents and chunks with LLM-powered intelligence.

### VisionTableEnricher ###

`VisionTableEnricher` is an `IngestionDocumentProcessor` that identifies table elements in the parsed document and sends them to a vision-capable LLM to extract well-formatted markdown tables.

```csharp
public VisionTableEnricher(IChatClient chatClient)
```

For each `IngestionDocumentTable` element, the enricher stores the LLM response in the element metadata under the key `"enriched_markdown_table"`.

### VisionOcrFallback ###

`VisionOcrFallback` is an `IngestionDocumentProcessor` that acts as a fallback for scanned pages or image-heavy regions. Elements with empty or whitespace-only text are sent to a vision LLM for OCR.

```csharp
public VisionOcrFallback(IChatClient chatClient)
```

When the LLM returns text, the processor updates `element.Text` with the OCR result and sets the metadata key `"ocr_source"` to `"vision_llm"`.

### ContextualChunkEnricher ###

`ContextualChunkEnricher` is an `IngestionChunkProcessor<string>` that generates a concise contextual summary for each chunk to improve search retrieval quality.

```csharp
public ContextualChunkEnricher(IChatClient chatClient)
```

```csharp
public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
    IAsyncEnumerable<IngestionChunk<string>> chunks,
    CancellationToken cancellationToken = default)
```

The summary is stored in chunk metadata under the key `"contextual_summary"`.

## Pipeline Composition ##

Processors are added to an `IngestionPipeline<T>` via the `DocumentProcessors` and `ChunkProcessors` collections:

    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.DataIngestion;
    using UglyToad.PdfPig.DataIngestion;
    using UglyToad.PdfPig.DataIngestion.Processors;

    IChatClient chatClient = /* your IChatClient implementation */;

    var reader = new PdfPigReader();

    var pipeline = new IngestionPipeline<string>(reader, chunker, writer)
    {
        DocumentProcessors =
        {
            new VisionTableEnricher(chatClient),
            new VisionOcrFallback(chatClient)
        },
        ChunkProcessors =
        {
            new ContextualChunkEnricher(chatClient)
        }
    };

    await pipeline.RunAsync("document.pdf");

Document processors run in the order they are added, so `VisionTableEnricher` enriches tables before `VisionOcrFallback` handles any remaining empty-text elements. Chunk processors run after chunking.

## Full Example ##

The following example shows a complete pipeline that reads a PDF with heuristic segmentation, enriches tables and OCR gaps, and adds contextual summaries to chunks:

    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.DataIngestion;
    using UglyToad.PdfPig.DataIngestion;
    using UglyToad.PdfPig.DataIngestion.Processors;
    using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

    // Configure the chat client (e.g. Azure OpenAI)
    IChatClient chatClient = new OpenAI.OpenAIClient("your-api-key")
        .GetChatClient("gpt-4o")
        .AsIChatClient();

    // Create reader with heuristic page segmentation
    var reader = new PdfPigReader(segmenter: HeuristicPageSegmenter.Instance);

    // Build the pipeline
    var pipeline = new IngestionPipeline<string>(reader, chunker, writer)
    {
        DocumentProcessors =
        {
            new VisionTableEnricher(chatClient),
            new VisionOcrFallback(chatClient)
        },
        ChunkProcessors =
        {
            new ContextualChunkEnricher(chatClient)
        }
    };

    await pipeline.RunAsync("report.pdf");

## See Also ##

+ [Document Layout Analysis](https://github.com/UglyToad/PdfPig/wiki/Document-Layout-Analysis) — details on page segmenters
+ [Microsoft.Extensions.DataIngestion overview](https://learn.microsoft.com/dotnet/ai/conceptual/medi-overview)
+ [Microsoft.Extensions.AI overview](https://learn.microsoft.com/dotnet/ai/conceptual/meai-overview)
