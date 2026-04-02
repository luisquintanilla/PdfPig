# Document Layout Analysis

_In computer vision, document layout analysis is the process of identifying and categorizing the regions of interest in the scanned image of a text document. A reading system requires the **segmentation of text zones** from **non-textual ones** and the arrangement in their correct **reading order**. Detection and **labeling of the different zones** (or blocks) as text body, illustrations, math symbols, and tables embedded in a document is called geometric layout analysis. But text zones play different logical roles inside the document (titles, captions, footnotes, etc.) and this kind of **semantic labeling** is the scope of the logical layout analysis._ – [Wikipedia](https://en.wikipedia.org/wiki/Document_layout_analysis)

In our case, we are using the pdf document itself instead of image representation. The following categories of tools are available:

- [**Word extractors**](#word-extractors)
- [**Page segmenters**](#page-segmenters)
- [**Reading order detectors**](#reading-order-detectors)
- [**Other layout tools**](#other-layout-tools)
- [**Export**](#export) – Viewing/exporting the results of document layout analysis

---

## Word extractors

Word extractors deal with the task of building words using letters in a page. 2 different methods are currently available:

- [**Default method**](#default-word-extractor)
- [**Nearest Neighbour**](#nearest-neighbour-method)

### Use cases

| Words | Default method | Nearest Neighbour |
|---|---|---|
| Horizontal | ✔️ | ✔️ |
| Axis aligned rotated | ✖️ | ✔️ |
| Rotated | ❌ | ✔️ |
| Curved | ❌ | ✔️ |

Legend: ✔️: supported, ✖️: partial support, ❌: not supported

### [Default word extractor](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig/Util/DefaultWordExtractor.cs)

TO DO

### [Nearest Neighbour method](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig.DocumentLayoutAnalysis/NearestNeighbourWordExtractor.cs)

#### Description

The nearest neighbour word extractor is useful to extract words from pdf documents with complex layouts. It will seek to connect each glyph bound box's `EndBaseLine` point with the closest glyph bound box `StartBaseLine` point. In order to decide whether two glyphs are _close enough_ from each other, the algorithm uses the maximum of both candidates width and point size as a reference distance.

- For glyphs with known text direction (axis aligned), the [Manhattan distance](https://en.wikipedia.org/wiki/Taxicab_geometry) is used and the threshold is set to 20% of the reference distance.
- For glyphs with unknown text direction, the [Euclidean distance](https://en.wikipedia.org/wiki/Euclidean_distance) is used and the threshold is set to 40% of the reference distance.

If the measured distance between the two glyphs is below this threshold, they are deemed to be connected.

Once glyphs are connected, they are then grouped to form words via a [depth first search algorithm](https://en.wikipedia.org/wiki/Depth-first_search).

#### Usage

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

using (var document = PdfDocument.Open(@"document.pdf"))
{
    for (var i = 0; i < document.NumberOfPages; i++)
    {
        var page = document.GetPage(i + 1);
        var words = page.GetWords(NearestNeighbourWordExtractor.Instance);

        foreach (var word in words)
        {
            // Do something
        }
    }
}
```

---

## Page segmenters

Page segmenters deal with the task of finding blocks of text in a page. They return a list of `TextBlock`s that can be thought of as paragraphs. Each `TextBlock` will contain the list of lines (`TextLine`) that belong to it. In turn, each `TextLine` contains the list of `Word`s that belong to it. Each of these elements have their own bounding box and text.

4 different methods are currently available:

- [**Default method**](#default-page-segmenter) – returns all words as a single block
- [**Recursive XY Cut**](#recursive-xy-cut-method) – a top-down method
- [**Docstrum for bounding boxes**](#docstrum-for-bounding-boxes-method) – a bottom-up method
- [**Heuristic Page Segmenter**](#heuristic-page-segmenter) – a font-size-aware heuristic method _(new)_

### Use cases

| Text | Default method | Recursive XY Cut | Docstrum | Heuristic |
|---|---|---|---|---|
| Single Column | ✔️ | ✔️ | ✔️ | ✔️ |
| Multi Columns | ✖️ | ✔️ | ✔️ | ✖️ |
| L-shaped text | ❌ | ❌ | ✔️ | ❌ |
| Rotated lines/paragraphs | ❌ | ✖️ | ✔️ | ❌ |
| Heading detection | ❌ | ❌ | ❌ | ✔️ |

Legend: ✔️: supported, ✖️: partial support, ❌: not supported

### [Default page segmenter](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig.DocumentLayoutAnalysis/DefaultPageSegmenter.cs)

#### Description

This method returns one single block, containing all words in the page.

#### Usage

```csharp
var blocks = DefaultPageSegmenter.Instance.GetBlocks(words);
```

### [Recursive XY Cut method](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig.DocumentLayoutAnalysis/RecursiveXYCut.cs)

#### Description

_The recursive X-Y cut is a top-down page segmentation technique that decomposes a document image recursively into a set of rectangular blocks._ – Wikipedia

The algorithm works in a top-down manner because it starts at the page level, then scans it from left to right to find a large enough gap between words. Once the gap is found, a vertical cut is made at this level and the page is now separated in two blocks. Then the algorithm starts scanning from bottom to top each block in order to find a large enough gap, and once it is found, a horizontal cut is made. This 2-step process is repeated until no cut is possible.

The height and width of the dominant font are used to decide vertical and horizontal gap sizes. A minimum block width can also be used to filter small blocks.

#### References

- [Wikipedia – Recursive X-Y cut](https://en.wikipedia.org/wiki/Recursive_X-Y_cut)
- [_Recursive X-Y Cut using Bounding Boxes of Connected Components_](https://www.researchgate.net/publication/220860850_Recursive_X-Y_cut_using_bounding_boxes_of_connected_components) by Jaekyu Ha, Robert M. Haralick and Ihsin T. Phillips

#### Usage

##### Simple case

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

using (var document = PdfDocument.Open("document.pdf"))
{
    for (var i = 0; i < document.NumberOfPages; i++)
    {
        var page = document.GetPage(i + 1);
        var words = page.GetWords();

        var blocks = RecursiveXYCut.Instance.GetBlocks(words);

        foreach (var block in blocks)
        {
            foreach (TextLine line in block.TextLines)
            {
                foreach (Word word in line.Words)
                {
                    Console.Write(word.Text + " ");
                }
            }
        }
    }
}
```

##### Advanced case

The method can be tailored by providing a **minimum block width**, and **horizontal and vertical gap sizes/functions**:

```csharp
var recursiveXYCut = new RecursiveXYCut(new RecursiveXYCut.RecursiveXYCutOptions()
{
    MinimumWidth = page.Width / 3.0,
    DominantFontWidthFunc = letters => letters.Select(l => l.BoundingBox.Width).Average(),
    DominantFontHeightFunc = letters => letters.Select(l => l.BoundingBox.Height).Average()
});

var blocks = recursiveXYCut.GetBlocks(words);
```

### [Docstrum for bounding boxes method](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig.DocumentLayoutAnalysis/DocstrumBoundingBoxes.cs)

#### Description

Paraphrasing the abstract of the original paper, _the document spectrum (or docstrum) is a method for structural page layout analysis based on bottom-up, nearest-neighbour clustering of page components. The method yields accurate within-line and between-line spacings, and locates text lines and text blocks._

This implementation loosely follows these steps, using only a subset of them, as we don't start from an image but from the document itself and don't need to estimate the skew. It works in a bottom-up fashion, starting at word level:

1. Estimate in-line and between-line spacing
2. Build lines of text (from words)
3. Build blocks of text (from text lines)

#### References

- [Wikipedia – Document layout analysis (bottom up approach)](https://en.wikipedia.org/wiki/Document_layout_analysis#Example_of_a_bottom_up_approach)
- [_The Document Spectrum for Page Layout Analysis_](https://dl.acm.org/citation.cfm?id=628542) by Lawrence O'Gorman

#### Usage

##### Simple case

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

using (var document = PdfDocument.Open(@"document.pdf"))
{
    for (var i = 0; i < document.NumberOfPages; i++)
    {
        var page = document.GetPage(i + 1);
        var words = page.GetWords();

        var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);

        foreach (var block in blocks)
        {
            // Do something
        }
    }
}
```

##### Advanced case

```csharp
var docstrum = new DocstrumBoundingBoxes(new DocstrumBoundingBoxes.DocstrumBoundingBoxesOptions()
{
    WithinLineBounds = new DocstrumBoundingBoxes.AngleBounds(-45, 45),
    BetweenLineBounds = new DocstrumBoundingBoxes.AngleBounds(35, 170),
    BetweenLineMultiplier = 1.5
});

var blocks = docstrum.GetBlocks(words);
```

### [Heuristic Page Segmenter](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig.DocumentLayoutAnalysis/PageSegmenter/HeuristicPageSegmenter.cs)

#### Description

The Heuristic Page Segmenter is a font-size-aware page segmenter that groups words into lines by baseline proximity and then groups consecutive lines into blocks based on vertical gaps and font-size transitions. Unlike other segmenters, it performs **heading detection** using a font-size histogram to identify the body font size and classify larger text as headings.

The algorithm works in the following steps:

1. **Font-size histogram → body size detection**: The font size of each word is computed (using the mode of its letters' point sizes). The most frequent font size across all words is identified as the _body font size_.
2. **Heading classification**: Any word whose font size exceeds `bodyFontSize × HeadingScaleThreshold` is classified as a heading.
3. **Baseline line grouping**: Words are grouped into lines by baseline Y proximity. Words whose baseline Y coordinates are within `averageLetterHeight × LineGapTolerance` of each other are placed on the same line. Words within each line are sorted left-to-right.
4. **Line sorting**: Lines are sorted top-to-bottom (descending Y in PDF coordinates).
5. **Block splitting on gaps and heading transitions**: Consecutive lines are merged into blocks. A new block boundary is created when either:
   - The vertical gap between consecutive lines exceeds `averageLineHeight × BlockGapMultiplier`, or
   - The heading classification changes between consecutive lines (i.e., a heading line is followed by a body line or vice versa).

#### Options

The segmenter is configured via `HeuristicPageSegmenterOptions`:

| Option | Type | Default | Description |
|---|---|---|---|
| `HeadingScaleThreshold` | `double` | `1.2` | Font size ratio above body font size to classify a word as a heading. A word is a heading if its font size > bodyFontSize × this value. |
| `LineGapTolerance` | `double` | `0.5` | Vertical gap tolerance for same-line grouping, as a multiple of average letter height. |
| `BlockGapMultiplier` | `double` | `1.5` | Gap multiplier for block boundary detection, as a multiple of average line height. |
| `WordSeparator` | `string` | `" "` | Separator used between words when building `TextLine` text. |
| `LineSeparator` | `string` | `"\n"` | Separator used between lines when building `TextBlock` text. |

#### Usage

##### Simple case

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

using var document = PdfDocument.Open("document.pdf");
var page = document.GetPage(1);
var words = page.GetWords();

// Default options
var blocks = HeuristicPageSegmenter.Instance.GetBlocks(words);

foreach (var block in blocks)
{
    Console.WriteLine(block.Text);
    Console.WriteLine("---");
}
```

##### Advanced case – custom options

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

// Increase heading threshold and block gap sensitivity
var options = new HeuristicPageSegmenter.HeuristicPageSegmenterOptions
{
    HeadingScaleThreshold = 1.3,
    BlockGapMultiplier = 2.0
};
var segmenter = new HeuristicPageSegmenter(options);

using var document = PdfDocument.Open("document.pdf");
var page = document.GetPage(1);
var words = page.GetWords();

var customBlocks = segmenter.GetBlocks(words);

foreach (var block in customBlocks)
{
    foreach (var line in block.TextLines)
    {
        Console.WriteLine(line.Text);
    }
    Console.WriteLine();
}
```

#### When to use Heuristic Page Segmenter vs. Recursive XY Cut

| Criteria | HeuristicPageSegmenter | RecursiveXYCut |
|---|---|---|
| **Best for** | Single-column documents with headings, reports, articles | Multi-column layouts, academic papers |
| **Heading detection** | ✔️ Built-in via font-size analysis | ❌ Not supported |
| **Multi-column support** | ✖️ Limited – processes lines top-to-bottom | ✔️ Splits columns via whitespace gaps |
| **Algorithm type** | Bottom-up (words → lines → blocks) | Top-down (page → recursive splits) |
| **Configuration** | Font-size thresholds and gap tolerances | Gap sizes and minimum block widths |
| **Rotated text** | ❌ Assumes horizontal layout | ✖️ Partial support |

**Use `HeuristicPageSegmenter`** when you need heading-aware block segmentation on documents with a straightforward single-column layout (e.g., reports, letters, simple articles). It will correctly split heading blocks from body text blocks based on font size.

**Use `RecursiveXYCut`** when your documents have multi-column layouts or complex spatial arrangements where whitespace-based splitting is more effective.

---

## Reading order detectors

Reading order detectors deal with the task of finding the blocks' reading order in a page. 3 different methods are currently available:

- [**Default Reading Order Detector**](#default-reading-order-detector) – returns blocks as-is
- [**Rendering Reading Order Detector**](#rendering-reading-order-detector) – uses `Letter.TextSequence` ordering
- [**Unsupervised Reading Order Detector**](#unsupervised-reading-order-detector) – spatial reasoning with Allen's interval relations

### [Default Reading Order Detector](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig.DocumentLayoutAnalysis/ReadingOrderDetector/DefaultReadingOrderDetector.cs)

#### Description

This reading order detector does nothing. It will return the blocks as they are provided and the `ReadingOrder` of each `TextBlock` remains -1.

#### Usage

```csharp
var orderedBlocks = DefaultReadingOrderDetector.Instance.Get(blocks);
```

### [Rendering Reading Order Detector](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig.DocumentLayoutAnalysis/ReadingOrderDetector/RenderingReadingOrderDetector.cs)

#### Description

This reading order detector uses the average of the `Letter.TextSequence` contained in each block to determine the reading order.

#### Usage

```csharp
var orderedBlocks = RenderingReadingOrderDetector.Instance.Get(blocks);
```

### [Unsupervised Reading Order Detector](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig.DocumentLayoutAnalysis/ReadingOrderDetector/UnsupervisedReadingOrderDetector.cs)

#### Description

Uses spatial reasoning based on Allen's interval algebra to determine column-wise reading order. Defines `BeforeInReading` and `BeforeInRendering` relations combined into a directed graph.

#### References

- Section 5.1 of [_Unsupervised document structure analysis of digital scientific articles_](http://www.know-center.tugraz.at/download_extern/papers/ijdl-2013.pdf) by S. Klampfl, M. Granitzer, K. Jack, R. Kern
- [Allen's interval algebra – Wikipedia](https://en.wikipedia.org/wiki/Allen%27s_interval_algebra)

#### Usage

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;

using (var document = PdfDocument.Open(@"document.pdf"))
{
    var page = document.GetPage(1);
    var words = page.GetWords();
    var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
    var orderedBlocks = UnsupervisedReadingOrderDetector.Instance.Get(blocks);

    foreach (var block in orderedBlocks)
    {
        // Do something
    }
}
```

---

## Other layout tools

- **Text edges** – TO DO
- **Whitespace coverage** – finds a cover of the background whitespace using maximal empty rectangles
- **Decoration Text Block Classifier** – classifies header/footer/decoration blocks

---

## Export

Results of document layout analysis can be exported using the following formats:

- **PAGE XML** – via `PageXmlTextExporter`
- **ALTO XML** – via `AltoXmlTextExporter`
- **hOCR** – via `HOcrTextExporter`

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Export;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;

using (var document = PdfDocument.Open(@"document.pdf"))
{
    var page = document.GetPage(1);

    // Example: export to PAGE XML
    var pageXml = new PageXmlTextExporter(
        NearestNeighbourWordExtractor.Instance,
        RecursiveXYCut.Instance,
        UnsupervisedReadingOrderDetector.Instance);
    var xml = pageXml.Get(page);
}
```
