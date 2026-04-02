# ONNX Layout Detection

ML-powered document layout analysis using ONNX Runtime. The `UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx` package brings deep-learning object detection models into the PdfPig page segmentation pipeline, enabling accurate identification of document regions such as text, tables, figures, headers, footers, and more.

## References

- [ONNX Runtime](https://onnxruntime.ai/)
- [RT-DETR v2 — Real-Time DEtection TRansformer](https://arxiv.org/abs/2304.08069)
- [Docling — Document Layout Analysis](https://github.com/DS4SD/docling) (source of the Heron model)

## Prerequisites

### NuGet Packages

| Package | Purpose |
|---------|---------|
| `UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx` | Core library — models, preprocessing, segmenter |
| `Microsoft.ML.OnnxRuntime` **or** `Microsoft.ML.OnnxRuntime.Gpu` | ONNX Runtime native binaries (CPU or GPU) |

> **Note:** The core library depends on `Microsoft.ML.OnnxRuntime.Managed` (the managed API surface). You must also install a **native runtime** package — either the CPU package (`Microsoft.ML.OnnxRuntime`) or the GPU package (`Microsoft.ML.OnnxRuntime.Gpu`) — for inference to work.

### ONNX Model File

You need an ONNX model file exported for document layout detection. The built-in `RtDetrLayoutModel` is designed for the **Docling RT-DETR Heron v2** model. You can also use any compatible ONNX detection model via `ConfigurableLayoutModel`.

## Architecture

### Model Abstraction

The package is built around the `ILayoutDetectionModel` interface, which encapsulates model-specific preprocessing and postprocessing:

```csharp
public interface ILayoutDetectionModel : IDisposable
{
    IReadOnlyList<NamedOnnxValue> Preprocess(SKBitmap pageImage, int originalWidth, int originalHeight);
    IReadOnlyList<LayoutDetection> Postprocess(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, int originalWidth, int originalHeight);
    IReadOnlyDictionary<int, string> LabelMapping { get; }
    string ModelPath { get; }
}
```

Two implementations are provided:

| Class | Description |
|-------|-------------|
| `RtDetrLayoutModel` | Purpose-built for the Docling RT-DETR v2 Heron model. Handles 640×640 exact resize, uint8 CHW tensor input with an `orig_target_sizes` tensor, and parses the model's `labels`/`boxes`/`scores` outputs. No NMS required (Hungarian matching is built in). |
| `ConfigurableLayoutModel` | A generic, config-driven model implementation powered by `LayoutModelOptions`. Supports letterboxing, float/uint8 tensors, ImageNet normalization, NMS, and multiple bounding-box output formats. Use this for YOLO-style and other custom ONNX models. |

### Key Types

| Type | Namespace | Description |
|------|-----------|-------------|
| `OnnxPageSegmenter` | `...Onnx` | Implements `IPageSegmenter` — drop-in replacement for rule-based segmenters |
| `OnnxSegmenterOptions` | `...Onnx` | Confidence threshold, render DPI, and ONNX `SessionOptions` |
| `LayoutDetection` | `...Onnx` | A detected region: `BoundingBox`, `Label`, `ClassId`, `Confidence` |
| `LayoutModelOptions` | `...Onnx.Models` | Full model config: input size, resize mode, pixel format, normalization, NMS, bbox format, class labels |
| `ImagePreprocessing` | `...Onnx` | Static helpers: `Letterbox`, `ResizeExact`, `ToChwUint8`, `ToChwFloat`, `NormalizeImageNet` |
| `DetectionPostprocessing` | `...Onnx` | Static helpers: `ApplyNms`, `ScaleToPage`, `ComputeIoU`, coordinate conversion utilities |
| `PageImageRenderer` | `...Onnx` | Renders word bounding boxes to an `SKBitmap` for model input |

## RT-DETR Label Classes

The `RtDetrLayoutModel` ships with a built-in label mapping for the 17 Docling Heron classes:

| Class ID | Label |
|----------|-------|
| 0 | `caption` |
| 1 | `footnote` |
| 2 | `formula` |
| 3 | `list_item` |
| 4 | `page_footer` |
| 5 | `page_header` |
| 6 | `picture` |
| 7 | `section_header` |
| 8 | `table` |
| 9 | `text` |
| 10 | `title` |
| 11 | `document_index` |
| 12 | `code` |
| 13 | `checkbox_selected` |
| 14 | `checkbox_unselected` |
| 15 | `form` |
| 16 | `key_value_region` |

## Usage

### Basic RT-DETR Usage

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Onnx.Models;

// Create the model and segmenter
using var model = new RtDetrLayoutModel("heron_v2.onnx");
using var segmenter = new OnnxPageSegmenter(model, new OnnxSegmenterOptions
{
    ConfidenceThreshold = 0.3f,
    RenderDpi = 150
});

// Open a PDF and segment a page
using var document = PdfDocument.Open("document.pdf");
var page = document.GetPage(1);
var blocks = segmenter.GetBlocks(page.GetWords());

foreach (var block in blocks)
{
    Console.WriteLine(block.Text);
}
```

### ConfigurableLayoutModel for Custom Models

Use `ConfigurableLayoutModel` with `LayoutModelOptions` when working with YOLO-style or other custom ONNX detection models:

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
        [0] = "text",
        [1] = "title",
        [2] = "figure",
        [3] = "table"
    }
};

using var model = new ConfigurableLayoutModel("custom_model.onnx", options);
using var segmenter = new OnnxPageSegmenter(model);
```

### OnnxSegmenterOptions

`OnnxSegmenterOptions` controls segmenter-level behavior (independent of the model):

```csharp
var segmenterOptions = new OnnxSegmenterOptions
{
    // Minimum confidence to keep a detection (default: 0.3)
    ConfidenceThreshold = 0.5f,

    // DPI for rendering the page image — higher improves accuracy but is slower (default: 150)
    RenderDpi = 200,

    // ONNX Runtime session options — use to configure GPU, thread count, etc.
    SessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions()
};
```

To enable GPU acceleration, configure `SessionOptions`:

```csharp
var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
sessionOptions.AppendExecutionProvider_CUDA(0); // requires Microsoft.ML.OnnxRuntime.Gpu

var segmenter = new OnnxPageSegmenter(model, new OnnxSegmenterOptions
{
    SessionOptions = sessionOptions
});
```

### Drop-in IPageSegmenter Replacement

`OnnxPageSegmenter` implements `IPageSegmenter`, so it can be used anywhere that interface is expected:

```csharp
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

IPageSegmenter segmenter = new OnnxPageSegmenter(model);

// Same API as DocstrumBoundingBoxes, RecursiveXYCut, etc.
var blocks = segmenter.GetBlocks(page.GetWords());
```

## Processing Pipeline

The `OnnxPageSegmenter.GetBlocks()` method executes the following pipeline:

```
PDF Words
  │
  ▼
1. Compute page bounds from word bounding boxes
  │
  ▼
2. Render page image (PageImageRenderer.RenderWords)
   └── Word bounding boxes → SKBitmap at configured DPI
  │
  ▼
3. Model preprocessing (ILayoutDetectionModel.Preprocess)
   └── Resize, tensor conversion, normalization
  │
  ▼
4. ONNX inference (InferenceSession.Run)
  │
  ▼
5. Model postprocessing (ILayoutDetectionModel.Postprocess)
   └── Parse outputs → LayoutDetection records
  │
  ▼
6. Confidence filtering
   └── Discard detections below threshold
  │
  ▼
7. Detection-to-word mapping (bounding box overlap)
   └── Each word assigned to the first overlapping detection
  │
  ▼
8. Group words into TextLines, then TextBlocks
   └── Unassigned words collected into a fallback block
```

### LayoutModelOptions Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `InputWidth` | `int` | 640 | Model input width in pixels |
| `InputHeight` | `int` | 640 | Model input height in pixels |
| `Resize` | `ResizeMode` | `Exact` | Resize strategy: `Exact`, `Letterbox`, or `AspectPreserve` |
| `PixelFormat` | `PixelFormat` | `Uint8Chw` | Tensor format: `Uint8Chw` or `Float32Chw` |
| `Normalization` | `ImageNormalization?` | `null` | Per-channel normalization (e.g., `WellKnownNormalizations.ImageNet`) |
| `ConfidenceThreshold` | `float` | 0.3 | Minimum detection confidence |
| `NmsIouThreshold` | `float` | 0.45 | IoU threshold for Non-Maximum Suppression |
| `RequiresNms` | `bool` | `false` | Whether to apply NMS to model output |
| `OutputBboxFormat` | `BboxFormat` | `CxCyWh` | Bounding box format: `CxCyWh`, `Xyxy`, or `Xywh` |
| `ClassLabels` | `IReadOnlyDictionary<int, string>?` | `null` | Class ID → label mapping |
