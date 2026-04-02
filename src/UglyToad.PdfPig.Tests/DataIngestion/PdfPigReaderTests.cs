#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DataIngestion;
using UglyToad.PdfPig.DataIngestion;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.Tests.Integration;
using Xunit;

namespace UglyToad.PdfPig.Tests.DataIngestion;

public class PdfPigReaderTests
{
    [Fact]
    public async Task ReadAsync_WithDefaultSegmenter_ReturnsDocumentWithSections()
    {
        var reader = new PdfPigReader();
        var path = IntegrationHelpers.GetDocumentPath("data");

        using var stream = File.OpenRead(path);
        var doc = await reader.ReadAsync(stream, "data.pdf", "application/pdf");

        Assert.NotNull(doc);
        Assert.Equal("data.pdf", doc.Identifier);
        Assert.NotEmpty(doc.Sections);
    }

    [Fact]
    public async Task ReadAsync_SectionCountMatchesPageCount()
    {
        var reader = new PdfPigReader();
        var path = IntegrationHelpers.GetDocumentPath("cat-genetics");

        int expectedPages;
        using (var pdfDoc = PdfDocument.Open(path))
        {
            expectedPages = pdfDoc.NumberOfPages;
        }

        using var stream = File.OpenRead(path);
        var doc = await reader.ReadAsync(stream, "cat-genetics.pdf", "application/pdf");

        Assert.Equal(expectedPages, doc.Sections.Count);
    }

    [Fact]
    public async Task ReadAsync_SectionsContainCorrectPageNumbers()
    {
        var reader = new PdfPigReader();
        var path = IntegrationHelpers.GetDocumentPath("data");

        using var stream = File.OpenRead(path);
        var doc = await reader.ReadAsync(stream, "data.pdf", "application/pdf");

        for (int i = 0; i < doc.Sections.Count; i++)
        {
            Assert.Equal(i + 1, doc.Sections[i].PageNumber);
        }
    }

    [Fact]
    public async Task ReadAsync_ParagraphsContainBoundingBoxMetadata()
    {
        var reader = new PdfPigReader();
        var path = IntegrationHelpers.GetDocumentPath("data");

        using var stream = File.OpenRead(path);
        var doc = await reader.ReadAsync(stream, "data.pdf", "application/pdf");

        var elementsWithMetadata = doc.EnumerateContent()
            .Where(e => e.HasMetadata)
            .ToList();

        Assert.NotEmpty(elementsWithMetadata);

        var first = elementsWithMetadata.First();
        Assert.True(first.Metadata.ContainsKey("BoundingBox.Left"));
        Assert.True(first.Metadata.ContainsKey("BoundingBox.Bottom"));
        Assert.True(first.Metadata.ContainsKey("BoundingBox.Right"));
        Assert.True(first.Metadata.ContainsKey("BoundingBox.Top"));
    }

    [Fact]
    public async Task ReadAsync_BoundingBoxValuesAreNumeric()
    {
        var reader = new PdfPigReader();
        var path = IntegrationHelpers.GetDocumentPath("data");

        using var stream = File.OpenRead(path);
        var doc = await reader.ReadAsync(stream, "data.pdf", "application/pdf");

        var element = doc.EnumerateContent().First(e => e.HasMetadata);

        Assert.IsType<double>(element.Metadata["BoundingBox.Left"]);
        Assert.IsType<double>(element.Metadata["BoundingBox.Bottom"]);
        Assert.IsType<double>(element.Metadata["BoundingBox.Right"]);
        Assert.IsType<double>(element.Metadata["BoundingBox.Top"]);
    }

    [Fact]
    public async Task ReadAsync_WithExplicitSegmenter_ReturnsStructuredSections()
    {
        var reader = new PdfPigReader(RecursiveXYCut.Instance);
        var path = IntegrationHelpers.GetDocumentPath("data");

        using var stream = File.OpenRead(path);
        var doc = await reader.ReadAsync(stream, "data.pdf", "application/pdf");

        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Sections);
    }

    [Fact]
    public async Task ReadAsync_MinimalPdf_ReturnsDocument()
    {
        var reader = new PdfPigReader();
        var path = IntegrationHelpers.GetDocumentPath("inherited_mediabox");

        using var stream = File.OpenRead(path);
        var doc = await reader.ReadAsync(stream, "inherited_mediabox.pdf", "application/pdf");

        Assert.NotNull(doc);
        Assert.Equal("inherited_mediabox.pdf", doc.Identifier);
    }

    [Fact]
    public async Task ReadAsync_CancellationAlreadyCancelled_ThrowsOperationCanceledException()
    {
        var reader = new PdfPigReader();
        var path = IntegrationHelpers.GetDocumentPath("data");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var stream = File.OpenRead(path);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.ReadAsync(stream, "data.pdf", "application/pdf", cts.Token));
    }

    [Fact]
    public async Task ReadAsync_ParagraphsHaveNonEmptyText()
    {
        var reader = new PdfPigReader();
        var path = IntegrationHelpers.GetDocumentPath("data");

        using var stream = File.OpenRead(path);
        var doc = await reader.ReadAsync(stream, "data.pdf", "application/pdf");

        var allElements = doc.EnumerateContent().ToList();

        foreach (var element in allElements)
        {
            Assert.False(string.IsNullOrEmpty(element.Text),
                "All paragraphs should have non-empty text (empty blocks are skipped).");
        }
    }

    [Fact]
    public async Task ReadAsync_ParagraphPageNumbersMatchSectionPageNumbers()
    {
        var reader = new PdfPigReader();
        var path = IntegrationHelpers.GetDocumentPath("data");

        using var stream = File.OpenRead(path);
        var doc = await reader.ReadAsync(stream, "data.pdf", "application/pdf");

        foreach (var section in doc.Sections)
        {
            foreach (var element in section.Elements)
            {
                Assert.Equal(section.PageNumber, element.PageNumber);
            }
        }
    }
}
#endif
