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
public PdfPigReader(
    IPageSegmenter? segmenter = null,
    PdfReadingMode mode = PdfReadingMode.TextOnly,
    int renderDpi = 150)
```

The optional `segmenter` parameter accepts any PdfPig page segmenter. If omitted it defaults to `DefaultPageSegmenter.Instance`. The `mode` parameter controls how pages are read and whether page images are rendered (see [Reading Modes](#reading-modes) below). The `renderDpi` controls the resolution of rendered page images when the mode includes rendering. Other built-in segmenter options include:

+ `HeuristicPageSegmenter.Instance`
+ `RecursiveXYCut` (see [Document Layout Analysis](https://github.com/UglyToad/PdfPig/wiki/Document-Layout-Analysis))
+ ONNX-based segmenters

### Reading Modes ###

The `PdfReadingMode` enum controls text extraction and page image rendering behavior:

| Mode | Text Extraction | Page Images | Scanned-Page Placeholders | Best For |
|------|----------------|-------------|--------------------------|----------|
| `TextOnly` (default) | ✅ Native text extraction | ❌ No rendering | ❌ No placeholders | Fast text-only pipelines — lowest memory usage |
| `Hybrid` | ✅ Native text extraction | ✅ Rendered at `renderDpi` | ✅ Placeholder for pages with no text | RAG pipelines with vision LLM enrichment (tables, OCR fallback) |
| `VisionOnly` | ❌ Skips text extraction | ✅ Rendered at `renderDpi` | ✅ Placeholder for every page | Pure VLM processing — let the vision model handle all content |

```csharp
// Default — text only, no images
var reader = new PdfPigReader();

// Hybrid — text + page images + scanned-page placeholders
var reader = new PdfPigReader(mode: PdfReadingMode.Hybrid);

// VisionOnly — skip text extraction, render every page as an image
var reader = new PdfPigReader(mode: PdfReadingMode.VisionOnly);
```

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

## Page Image Rendering ##

When `PdfReadingMode.Hybrid` or `PdfReadingMode.VisionOnly` is used, `PdfPigReader` renders each page to a PNG image at the specified `renderDpi`. The rendered images are stored in section metadata so that downstream vision processors can send actual page images to LLMs instead of text-only prompts.

### Metadata Keys ###

| Key | Type | Description |
|-----|------|-------------|
| `page_image` | `byte[]` | PNG-encoded image of the full page |
| `page_width` | `double` | Page width in PDF points |
| `page_height` | `double` | Page height in PDF points |

### Controlling Page Rendering ###

```csharp
// Hybrid mode — text extraction + page images at 150 DPI
var reader = new PdfPigReader(
    segmenter: HeuristicPageSegmenter.Instance,
    mode: PdfReadingMode.Hybrid,
    renderDpi: 150);

// VisionOnly mode — skip text extraction, render every page
var visionReader = new PdfPigReader(mode: PdfReadingMode.VisionOnly);

// TextOnly mode (default) — no page images, fastest processing
var lightReader = new PdfPigReader(mode: PdfReadingMode.TextOnly);
```

### Direct Page Rendering ###

`PageImageRenderer` can be used independently of `PdfPigReader` when you need to render pages or specific regions on demand:

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DataIngestion;

using var document = PdfDocument.Open("doc.pdf");
var page = document.GetPage(1);

// Render the full page at 200 DPI
byte[] pageImage = PageImageRenderer.RenderPage(page, dpi: 200);

// Render a specific region (e.g. a detected table bounding box)
byte[] regionImage = PageImageRenderer.RenderRegion(page, someRegion, dpi: 200);
```

## Processors ##

The package includes three processors that leverage an `IChatClient` (from Microsoft.Extensions.AI) to enrich documents and chunks with LLM-powered intelligence.

### VisionTableEnricher ###

`VisionTableEnricher` is an `IngestionDocumentProcessor` that identifies table elements in the parsed document and sends them to a vision-capable LLM to extract well-formatted markdown tables.

```csharp
public VisionTableEnricher(IChatClient chatClient)
```

For each `IngestionDocumentTable` element, the enricher stores the LLM response in the element metadata under the key `"enriched_markdown_table"`.

When a page image is available in `section.Metadata["page_image"]`, the enricher sends the actual image to the LLM via MEAI's `DataContent(imageBytes, "image/png")` alongside a `TextContent` prompt — enabling true vision-based table extraction. If no page image is present, it gracefully falls back to a text-based prompt.

### VisionOcrEnricher ###

`VisionOcrEnricher` is an `IngestionDocumentProcessor` that acts as a fallback for scanned pages or image-heavy regions. Elements with empty or whitespace-only text are sent to a vision LLM for OCR.

```csharp
public VisionOcrEnricher(IChatClient chatClient)
```

When the LLM returns text, the processor updates `element.Text` with the OCR result and sets the metadata key `"ocr_source"` to `"vision_llm"`.

When a page image is available in section metadata, `VisionOcrEnricher` sends the rendered page image via `DataContent(imageBytes, "image/png")` combined with `TextContent` for true vision OCR. If no page image is present, it falls back to a text-only prompt describing the region.

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

    // Hybrid mode — text extraction + page images for vision processors
    var reader = new PdfPigReader(mode: PdfReadingMode.Hybrid);

    var pipeline = new IngestionPipeline<string>(reader, chunker, writer)
    {
        DocumentProcessors =
        {
            new VisionTableEnricher(chatClient),
            new VisionOcrEnricher(chatClient)
        },
        ChunkProcessors =
        {
            new ContextualChunkEnricher(chatClient)
        }
    };

    await pipeline.RunAsync("document.pdf");

Document processors run in the order they are added, so `VisionTableEnricher` enriches tables before `VisionOcrEnricher` handles any remaining empty-text elements. Chunk processors run after chunking. Both vision processors automatically use page images from section metadata when available, sending multi-content messages (`DataContent` + `TextContent`) to the LLM.

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

    // Create reader with heuristic page segmentation in Hybrid mode
    var reader = new PdfPigReader(
        segmenter: HeuristicPageSegmenter.Instance,
        mode: PdfReadingMode.Hybrid,
        renderDpi: 150);

    // Build the pipeline — vision processors use page images automatically
    var pipeline = new IngestionPipeline<string>(reader, chunker, writer)
    {
        DocumentProcessors =
        {
            new VisionTableEnricher(chatClient),
            new VisionOcrEnricher(chatClient)
        },
        ChunkProcessors =
        {
            new ContextualChunkEnricher(chatClient)
        }
    };

    await pipeline.RunAsync("report.pdf");

## Scanned PDF Support ##

`PdfPigReader` automatically detects scanned or image-only pages that contain no extractable text. When `PdfReadingMode.Hybrid` is used, these pages receive a **placeholder element** — an `IngestionDocumentParagraph` with empty text and `Metadata["placeholder"] = true`. In `VisionOnly` mode, every page receives a placeholder since text extraction is skipped entirely.

### How It Works ###

1. `PdfPigReader` calls `page.GetWords()`. For scanned pages this returns no words.
2. The segmenter produces no text blocks, so no elements are created.
3. In `Hybrid` mode, if the section has zero elements, a placeholder element is inserted with `Text = ""`, the correct `PageNumber`, and `Metadata["placeholder"] = true`. In `VisionOnly` mode, a placeholder is always inserted for every page.
4. The page image is still rendered and stored in `section.Metadata["page_image"]`.
5. `VisionOcrEnricher` finds the placeholder (empty text), retrieves the page image from section metadata, and sends it to a vision LLM for OCR.
6. The OCR result is stored in the placeholder's `Text` property and `Metadata["ocr_source"]` is set to `"vision_llm"`.

### Pipeline Flow ###

| Page Type | GetWords() | Placeholder? | VisionOcrEnricher |
|-----------|-----------|-------------|-------------------|
| Digital (has text) | Returns words | No (elements already exist) | Skips (text present) |
| Scanned (image-only) | Empty | Yes | Processes via vision LLM OCR |
| Mixed PDF | Per-page | Only blank pages | Only blank pages |

### Example ###

No code changes are needed — placeholder creation and OCR enrichment happen automatically:

    var reader = new PdfPigReader(mode: PdfReadingMode.Hybrid);
    using var stream = File.OpenRead("scanned-document.pdf");

    var doc = await reader.ReadAsync(stream, "scanned.pdf", "application/pdf");

    // Placeholders are created for scanned pages — enrich them with VisionOcrEnricher
    IChatClient chatClient = /* your vision-capable IChatClient */;
    var ocrEnricher = new VisionOcrEnricher(chatClient);
    doc = await ocrEnricher.ProcessAsync(doc);

    // Now all pages have text — both digital and OCR'd scanned pages
    foreach (var element in doc.EnumerateContent())
    {
        Console.WriteLine(element.Text);
    }

## See Also ##

+ [Document Layout Analysis](https://github.com/UglyToad/PdfPig/wiki/Document-Layout-Analysis) — details on page segmenters
+ [Microsoft.Extensions.DataIngestion overview](https://learn.microsoft.com/dotnet/ai/conceptual/medi-overview)
+ [Microsoft.Extensions.AI overview](https://learn.microsoft.com/dotnet/ai/conceptual/meai-overview)
