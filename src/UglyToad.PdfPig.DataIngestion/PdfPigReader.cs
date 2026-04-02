namespace UglyToad.PdfPig.DataIngestion
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DataIngestion;
    using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

    /// <summary>
    /// Reads PDF documents using PdfPig and converts them to MEDI <see cref="IngestionDocument"/> format.
    /// Supports pluggable page segmentation via <see cref="IPageSegmenter"/>.
    /// </summary>
    public class PdfPigReader : IngestionDocumentReader
    {
        private readonly IPageSegmenter segmenter;
        private readonly bool renderPageImages;
        private readonly int renderDpi;

        /// <summary>
        /// Creates a new <see cref="PdfPigReader"/>.
        /// </summary>
        /// <param name="segmenter">
        /// Page segmenter for layout analysis. Defaults to <see cref="DefaultPageSegmenter"/> if <see langword="null"/>.
        /// </param>
        /// <param name="renderPageImages">
        /// Whether to render each page as a PNG image and store it in section metadata. Defaults to <see langword="true"/>.
        /// </param>
        /// <param name="renderDpi">
        /// The DPI to use when rendering page images. Defaults to 150.
        /// </param>
        public PdfPigReader(IPageSegmenter? segmenter = null, bool renderPageImages = true, int renderDpi = 150)
        {
            this.segmenter = segmenter ?? DefaultPageSegmenter.Instance;
            this.renderPageImages = renderPageImages;
            this.renderDpi = renderDpi;
        }

        /// <inheritdoc/>
        public override async Task<IngestionDocument> ReadAsync(
            Stream source, string identifier, string mediaType, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);

            using var pdfDocument = PdfDocument.Open(source);
            var document = new IngestionDocument(identifier);

            for (var i = 1; i <= pdfDocument.NumberOfPages; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = pdfDocument.GetPage(i);
                var words = page.GetWords();
                var blocks = segmenter.GetBlocks(words);

                var section = new IngestionDocumentSection
                {
                    PageNumber = i
                };

                if (renderPageImages)
                {
                    var imageBytes = PageImageRenderer.RenderPage(page, renderDpi);
                    section.Metadata["page_image"] = imageBytes;
                    section.Metadata["page_width"] = page.Width;
                    section.Metadata["page_height"] = page.Height;
                }

                foreach (var block in blocks)
                {
                    if (string.IsNullOrEmpty(block.Text))
                    {
                        continue;
                    }

                    var paragraph = new IngestionDocumentParagraph(block.Text)
                    {
                        Text = block.Text,
                        PageNumber = i
                    };

                    var bbox = block.BoundingBox;
                    paragraph.Metadata["BoundingBox.Left"] = bbox.Left;
                    paragraph.Metadata["BoundingBox.Bottom"] = bbox.Bottom;
                    paragraph.Metadata["BoundingBox.Right"] = bbox.Right;
                    paragraph.Metadata["BoundingBox.Top"] = bbox.Top;

                    section.Elements.Add(paragraph);
                }

                document.Sections.Add(section);
            }

            return document;
        }
    }
}