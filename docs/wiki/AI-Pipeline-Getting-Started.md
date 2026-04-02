# AI Pipeline Getting Started

This guide walks through five progressive levels of PDF processing with PdfPig — from basic text extraction to a full LLM-enriched RAG pipeline. Each level builds on the previous one.

## Decision Flowchart

```
Need PDF text?
  └── Yes → Level 1: Basic Text Extraction

Need structure (headings, paragraphs)?
  └── Yes → Level 2: Heuristic Layout

Need ML-precise layout (tables, figures, headers, code)?
  └── Yes → Level 3: ML Layout Detection

Building a RAG pipeline?
  └── Yes → Level 4: MEDI Integration

Need table markdown / OCR fallback / contextual chunks?
  └── Yes → Level 5: LLM Enrichment
```

---

## Level 1: Basic Text Extraction

**What it does:** Extracts text and words from PDF pages using PdfPig core. No layout analysis beyond basic word grouping.

### NuGet Packages

```
Install-Package PdfPig
```

### Code Example

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

using var document = PdfDocument.Open("document.pdf");

foreach (var page in document.GetPages())
{
    // Get text in content order (recommended over page.Text)
    string text = ContentOrderTextExtractor.GetText(page);
    Console.WriteLine(text);
}
```

> **Tip:** At this level you're using PdfPig core directly (no DataIngestion). Page image rendering is a DataIngestion feature — see Level 4+.

### When to Use

- You need raw text from a PDF with simple, single-column content.
- You don't need to distinguish headings from body text.
- Quick text dumps or full-text search indexing of simple documents.
- Use `renderPageImages: false` if you later move to Level 4 but only need text extraction.

---

## Level 2: Heuristic Layout

**What it adds:** Font-size-aware page segmentation that detects headings and groups words into structured blocks (paragraphs, headings). No ML models required.

### NuGet Packages

```
Install-Package PdfPig
```

> `HeuristicPageSegmenter` is included in the `UglyToad.PdfPig.DocumentLayoutAnalysis` assembly, which ships with the core `PdfPig` package.

### Code Example

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

using var document = PdfDocument.Open("document.pdf");
var page = document.GetPage(1);
var words = page.GetWords();

// Heuristic segmenter detects headings via font-size analysis
var blocks = HeuristicPageSegmenter.Instance.GetBlocks(words);

foreach (var block in blocks)
{
    Console.WriteLine($"[Block] {block.Text}");
    Console.WriteLine("---");
}
```

#### Custom Options

Tune heading detection and block splitting thresholds:

```csharp
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

var options = new HeuristicPageSegmenter.HeuristicPageSegmenterOptions
{
    HeadingScaleThreshold = 1.3,  // font size ratio to classify as heading
    BlockGapMultiplier = 2.0,     // vertical gap multiplier for block boundaries
    LineGapTolerance = 0.5        // baseline proximity tolerance for same-line grouping
};
var segmenter = new HeuristicPageSegmenter(options);

var blocks = segmenter.GetBlocks(words);
```

### When to Use

- Single-column documents with headings (reports, letters, articles).
- You want heading/body distinction without ML model overhead.
- Fast, zero-dependency layout analysis.
- Page image rendering is optional at this level — enable it in Level 4+ if you plan to use vision LLM processors.

See [Document-Layout-Analysis § Heuristic Page Segmenter](Document-Layout-Analysis#heuristic-page-segmenter) for full details.

---

## Level 3: ML Layout Detection

**What it adds:** Deep-learning object detection using ONNX Runtime. Identifies 17 document region types (text, tables, figures, section headers, code, formulas, footnotes, etc.) with bounding-box precision.

### NuGet Packages

```
Install-Package UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx
Install-Package Microsoft.ML.OnnxRuntime          # CPU inference
# or: Install-Package Microsoft.ML.OnnxRuntime.Gpu  # GPU inference
```

You also need an ONNX model file (e.g., the Docling RT-DETR Heron v2 model).

### Code Example

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx.Models;

// Create the RT-DETR model and ONNX segmenter
using var model = new RtDetrLayoutModel("heron_v2.onnx");
using var segmenter = new OnnxPageSegmenter(model, new OnnxSegmenterOptions
{
    ConfidenceThreshold = 0.3f,
    RenderDpi = 150
});

// Segment a page
using var document = PdfDocument.Open("document.pdf");
var page = document.GetPage(1);
var blocks = segmenter.GetBlocks(page.GetWords());

foreach (var block in blocks)
{
    Console.WriteLine($"[{block.Tag}] {block.Text}");
}
```

#### Using a Custom ONNX Model

For YOLO-style or other custom models, use `ConfigurableLayoutModel`:

```csharp
using UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx.Models;

var options = new LayoutModelOptions
{
    InputWidth = 640,
    InputHeight = 640,
    Resize = ResizeMode.Letterbox,
    PixelFormat = PixelFormat.Float32Chw,
    Normalization = WellKnownNormalizations.ImageNet,
    OutputBboxFormat = BboxFormat.CxCyWh,
    ConfidenceThreshold = 0.25f,
    RequiresNms = true,
    NmsIouThreshold = 0.45f,
    ClassLabels = new Dictionary<int, string>
    {
        [0] = "text", [1] = "title", [2] = "figure", [3] = "table"
    }
};

using var model = new ConfigurableLayoutModel("custom_model.onnx", options);
using var segmenter = new OnnxPageSegmenter(model);
```

### When to Use

- Complex multi-region documents (academic papers, technical manuals).
- You need to distinguish tables, figures, code blocks, and formulas from body text.
- Accuracy matters more than speed — ML inference adds latency per page.

See [ONNX-Layout-Detection](ONNX-Layout-Detection) for the full reference.

---

## Level 4: MEDI Integration

**What it adds:** Integration with [Microsoft.Extensions.DataIngestion](https://learn.microsoft.com/dotnet/ai/conceptual/medi-overview) (MEDI). `PdfPigReader` acts as an `IngestionDocumentReader` that plugs into MEDI pipelines — producing structured `IngestionDocument` objects with sections and elements.

### NuGet Packages

```
Install-Package UglyToad.PdfPig.DataIngestion
```

### Code Example

```csharp
using UglyToad.PdfPig.DataIngestion;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

// Create reader with page image rendering enabled for vision processor support
var reader = new PdfPigReader(
    segmenter: HeuristicPageSegmenter.Instance,
    renderPageImages: true,
    renderDpi: 150);
using var stream = File.OpenRead("document.pdf");

var doc = await reader.ReadAsync(stream, "document.pdf", "application/pdf");

foreach (var section in doc.Sections)
{
    Console.WriteLine($"--- Page Section ---");

    // Access the rendered page image if needed
    if (section.Metadata.TryGetValue("page_image", out var imgObj))
    {
        byte[] pageImage = (byte[])imgObj;
        Console.WriteLine($"  Page image: {pageImage.Length} bytes");
    }

    foreach (var element in section.Elements)
    {
        Console.WriteLine(element.Text);
    }
}
```

#### With ONNX Segmenter

Combine Level 3 and Level 4 by injecting the ONNX segmenter:

```csharp
using UglyToad.PdfPig.DataIngestion;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx.Models;

using var model = new RtDetrLayoutModel("heron_v2.onnx");
using var segmenter = new OnnxPageSegmenter(model);

// Enable page images for downstream vision processors
var reader = new PdfPigReader(segmenter: segmenter, renderPageImages: true);
using var stream = File.OpenRead("document.pdf");

var doc = await reader.ReadAsync(stream, "document.pdf", "application/pdf");
```

### When to Use

- You're building a RAG pipeline with MEDI as the orchestration layer.
- You want structured `IngestionDocument` output that feeds into chunkers and vector stores.
- You need a composable pipeline with pluggable segmenters.
- Use `renderPageImages: true` (the default) if you plan to use vision LLM enrichment in Level 5.
- Use `renderPageImages: false` for text-only pipelines where you want faster processing and lower memory usage.

See [Data-Ingestion](Data-Ingestion) for full details.

---

## Level 5: LLM Enrichment

**What it adds:** LLM-powered document and chunk processors that run within the MEDI pipeline. Three processors are available:

| Processor | Type | Purpose |
|-----------|------|---------|
| `VisionTableEnricher` | `IngestionDocumentProcessor` | Sends table elements to a vision LLM to extract well-formatted markdown tables |
| `VisionOcrEnricher` | `IngestionDocumentProcessor` | Falls back to a vision LLM for OCR on elements with empty text (scanned pages) |
| `ContextualChunkEnricher` | `IngestionChunkProcessor<string>` | Generates a contextual summary for each chunk to improve retrieval quality |

### NuGet Packages

```
Install-Package UglyToad.PdfPig.DataIngestion
Install-Package Microsoft.Extensions.AI.OpenAI    # or another IChatClient provider
```

### Code Example

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using UglyToad.PdfPig.DataIngestion;
using UglyToad.PdfPig.DataIngestion.Processors;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

// Configure the chat client (e.g. Azure OpenAI with a vision-capable model)
IChatClient chatClient = new OpenAI.OpenAIClient("your-api-key")
    .GetChatClient("gpt-4o")
    .AsIChatClient();

// Create reader with page image rendering for vision processor support
var reader = new PdfPigReader(
    segmenter: HeuristicPageSegmenter.Instance,
    renderPageImages: true,   // renders each page as PNG for vision LLMs
    renderDpi: 150);

// Build the pipeline with vision-enabled LLM processors
var pipeline = new IngestionPipeline<string>(reader, chunker, writer)
{
    DocumentProcessors =
    {
        // Sends actual page images via DataContent to the LLM
        new VisionTableEnricher(chatClient),   // enrich tables → markdown
        new VisionOcrEnricher(chatClient)       // OCR empty-text elements
    },
    ChunkProcessors =
    {
        new ContextualChunkEnricher(chatClient) // add contextual summaries
    }
};

await pipeline.RunAsync("report.pdf");
```

### Processor Details

**VisionTableEnricher** — For each `IngestionDocumentTable` element, sends the actual rendered page image (via MEAI's `DataContent(imageBytes, "image/png")` + `TextContent`) to a vision-capable LLM and stores the result in element metadata under the key `"enriched_markdown_table"`. Falls back to a text-based prompt if no page image is available.

**VisionOcrEnricher** — Elements with empty or whitespace-only text are sent to a vision LLM for OCR using the rendered page image. Updates `element.Text` with the OCR result and sets metadata key `"ocr_source"` to `"vision_llm"`. Falls back to a text-only prompt if no page image is in section metadata.

**ContextualChunkEnricher** — Generates a concise contextual summary for each text chunk. Stored in chunk metadata under the key `"contextual_summary"`. This improves search retrieval by adding semantic context.

### When to Use

- Your PDFs contain tables that need markdown representation for downstream LLM use.
- You have scanned or image-heavy PDFs where text extraction yields empty content.
- You want richer chunk metadata to improve vector search recall in RAG.
- Levels 4+5 benefit from page image rendering (`renderPageImages: true`) for true vision LLM integration — the processors send multi-content messages with actual page images.
- Use `renderPageImages: false` for text-only pipelines where speed and lower memory usage are priorities.

See [Data-Ingestion § Processors](Data-Ingestion#processors) for full processor documentation.

---

## Summary

| Level | Package(s) | Adds | Best For |
|-------|-----------|------|----------|
| 1 | `PdfPig` | Text extraction | Simple text dumps |
| 2 | `PdfPig` | Heading/paragraph detection | Structured single-column docs |
| 3 | `PdfPig` + `...Onnx` + ONNX Runtime | 17-class ML layout detection | Complex multi-region layouts |
| 4 | `PdfPig` + `...DataIngestion` | MEDI pipeline reader + page images | RAG pipeline ingestion |
| 5 | `PdfPig` + `...DataIngestion` + `M.E.AI` | Vision table/OCR/context enrichment | Production RAG with vision LLMs |

## See Also

- [Document-Layout-Analysis](Document-Layout-Analysis) — Word extractors, page segmenters, reading order, export
- [ONNX-Layout-Detection](ONNX-Layout-Detection) — RT-DETR model setup, ConfigurableLayoutModel, GPU
- [Data-Ingestion](Data-Ingestion) — PdfPigReader, processors, pipeline composition
