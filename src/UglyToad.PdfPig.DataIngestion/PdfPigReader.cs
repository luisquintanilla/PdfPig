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

        /// <summary>
        /// Creates a new <see cref="PdfPigReader"/>.
        /// </summary>
        /// <param name="segmenter">
        /// Page segmenter for layout analysis. Defaults to <see cref="DefaultPageSegmenter"/> if <see langword="null"/>.
        /// </param>
        public PdfPigReader(IPageSegmenter? segmenter = null)
        {
            this.segmenter = segmenter ?? DefaultPageSegmenter.Instance;
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