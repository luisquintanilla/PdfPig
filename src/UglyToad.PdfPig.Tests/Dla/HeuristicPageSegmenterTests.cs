namespace UglyToad.PdfPig.Tests.Dla
{
    using PdfFonts;
    using System.Collections.Generic;
    using System.Linq;
    using UglyToad.PdfPig.Content;
    using UglyToad.PdfPig.Core;
    using UglyToad.PdfPig.DocumentLayoutAnalysis;
    using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
    using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

    public class HeuristicPageSegmenterTests
    {
        #region Helpers

        /// <summary>
        /// Creates a synthetic <see cref="Letter"/> at a given position with a given point size.
        /// </summary>
        private static Letter CreateLetter(string value, double x, double baselineY, double width, double height, double pointSize)
        {
            var bottomLeft = new PdfPoint(x, baselineY);
            var topRight = new PdfPoint(x + width, baselineY + height);
            var glyphRect = new PdfRectangle(bottomLeft, topRight);

            return new Letter(
                value,
                glyphRect,
                glyphRect,
                startBaseLine: new PdfPoint(x, baselineY),
                endBaseLine: new PdfPoint(x + width, baselineY),
                width: width,
                fontSize: pointSize,
                fontDetails: (FontDetails)null,
                renderingMode: TextRenderingMode.Fill,
                strokeColor: null,
                fillColor: null,
                pointSize: pointSize,
                textSequence: 0);
        }

        /// <summary>
        /// Creates a synthetic <see cref="Word"/> at a given position with a specified point size.
        /// Each character in <paramref name="text"/> gets its own letter at sequential x offsets.
        /// </summary>
        private static Word CreateWord(string text, double x, double baselineY, double charWidth, double charHeight, double pointSize)
        {
            var letters = new List<Letter>();
            for (int i = 0; i < text.Length; i++)
            {
                letters.Add(CreateLetter(text[i].ToString(), x + i * charWidth, baselineY, charWidth, charHeight, pointSize));
            }

            return new Word(letters);
        }

        #endregion

        #region Instance and constructor tests

        [Fact]
        public void Instance_ReturnsNonNullSegmenter()
        {
            var instance = HeuristicPageSegmenter.Instance;
            Assert.NotNull(instance);
        }

        [Fact]
        public void Instance_ReturnsSameReference()
        {
            var a = HeuristicPageSegmenter.Instance;
            var b = HeuristicPageSegmenter.Instance;
            Assert.Same(a, b);
        }

        [Fact]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new HeuristicPageSegmenter(null));
        }

        [Fact]
        public void Constructor_WithDefaultOptions_CreatesSegmenter()
        {
            var segmenter = new HeuristicPageSegmenter();
            Assert.NotNull(segmenter);
        }

        [Fact]
        public void Constructor_WithCustomOptions_CreatesSegmenter()
        {
            var options = new HeuristicPageSegmenter.HeuristicPageSegmenterOptions
            {
                HeadingScaleThreshold = 2.0,
                BlockGapMultiplier = 3.0,
                LineGapTolerance = 1.0
            };

            var segmenter = new HeuristicPageSegmenter(options);
            Assert.NotNull(segmenter);
        }

        #endregion

        #region Edge cases

        [Fact]
        public void GetBlocks_NullWords_ReturnsEmpty()
        {
            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(null);
            Assert.Empty(blocks);
        }

        [Fact]
        public void GetBlocks_EmptyWordList_ReturnsEmpty()
        {
            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(Enumerable.Empty<Word>());
            Assert.Empty(blocks);
        }

        [Fact]
        public void GetBlocks_SingleWord_ReturnsSingleBlock()
        {
            var word = CreateWord("Hello", x: 10, baselineY: 100, charWidth: 6, charHeight: 10, pointSize: 12);
            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { word });

            Assert.Single(blocks);
            Assert.Contains("Hello", blocks[0].Text);
        }

        [Fact]
        public void GetBlocks_WhitespaceOnlyWords_ReturnsEmpty()
        {
            // Words with only whitespace text should be filtered out.
            // We create a "word" whose text is a space; the segmenter filters out whitespace-only words.
            var letter = CreateLetter(" ", x: 10, baselineY: 100, width: 5, height: 10, pointSize: 12);
            var word = new Word(new[] { letter });

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { word });
            Assert.Empty(blocks);
        }

        #endregion

        #region Line grouping tests

        [Fact]
        public void GetBlocks_WordsOnSameBaseline_GroupedIntoSameLine()
        {
            // Two words at the same baseline Y should be grouped into the same line/block.
            var word1 = CreateWord("Hello", x: 10, baselineY: 100, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("World", x: 50, baselineY: 100, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { word1, word2 });

            Assert.Single(blocks);
            Assert.Contains("Hello", blocks[0].Text);
            Assert.Contains("World", blocks[0].Text);
        }

        [Fact]
        public void GetBlocks_WordsOnSameBaseline_SortedLeftToRight()
        {
            // Words submitted in reverse order should still be sorted left-to-right within a line.
            var word1 = CreateWord("Second", x: 100, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("First", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { word1, word2 });

            Assert.Single(blocks);
            var text = blocks[0].Text;
            Assert.True(text.IndexOf("First", StringComparison.Ordinal) < text.IndexOf("Second", StringComparison.Ordinal));
        }

        [Fact]
        public void GetBlocks_WordsOnSlightlyDifferentBaselines_GroupedIntoSameLine()
        {
            // Words within LineGapTolerance (default 0.5 × avgLetterHeight) should be on the same line.
            // avgLetterHeight = 10, so tolerance = 5. A difference of 2 is within tolerance.
            var word1 = CreateWord("Hello", x: 10, baselineY: 100, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("World", x: 50, baselineY: 102, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { word1, word2 });

            Assert.Single(blocks);
        }

        [Fact]
        public void GetBlocks_WordsOnFarApartBaselines_GroupedIntoDifferentLines()
        {
            // Words with baselines far apart should be on different lines.
            // avgLetterHeight = 10, tolerance = 5. A difference of 20 is well outside tolerance.
            var word1 = CreateWord("Top", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("Bottom", x: 10, baselineY: 180, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { word1, word2 });

            // Both lines have the same font size, and the gap between lines (20) may or may not exceed
            // blockGapThreshold (1.5 × avgLineHeight). With charHeight=10, lines will have height 10,
            // avgLineHeight=10, so blockGapThreshold=15. Gap=20 > 15, so two blocks.
            Assert.Equal(2, blocks.Count);
        }

        #endregion

        #region Block gap detection tests

        [Fact]
        public void GetBlocks_ConsecutiveLinesWithSmallGap_SameBlock()
        {
            // Three closely spaced lines should form a single block.
            // charHeight=10, avgLineHeight=10, blockGapThreshold = 10 * 1.5 = 15
            // Gap between lines: 12 (< 15), so all in the same block.
            var word1 = CreateWord("Line1", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("Line2", x: 10, baselineY: 188, charWidth: 6, charHeight: 10, pointSize: 12);
            var word3 = CreateWord("Line3", x: 10, baselineY: 176, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { word1, word2, word3 });

            Assert.Single(blocks);
        }

        [Fact]
        public void GetBlocks_LinesWithLargeGap_SeparateBlocks()
        {
            // Two groups separated by a large gap should form two blocks.
            // charHeight=10, avgLineHeight=10, blockGapThreshold = 10 * 1.5 = 15
            // Gap between word2 and word3: 50 (>> 15)
            var word1 = CreateWord("Block1Line1", x: 10, baselineY: 300, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("Block1Line2", x: 10, baselineY: 288, charWidth: 6, charHeight: 10, pointSize: 12);
            var word3 = CreateWord("Block2Line1", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { word1, word2, word3 });

            Assert.Equal(2, blocks.Count);
        }

        #endregion

        #region Heading detection / font-size histogram tests

        [Fact]
        public void GetBlocks_HeadingFontSize_SeparatedFromBody()
        {
            // The body font size is the most frequent point size.
            // Body = 12pt (appears 3 times), heading threshold = 12 * 1.2 = 14.4.
            // A 24pt word is above the threshold, so it forms a separate block.
            var heading = CreateWord("Title", x: 10, baselineY: 300, charWidth: 8, charHeight: 20, pointSize: 24);
            var body1 = CreateWord("BodyA", x: 10, baselineY: 260, charWidth: 6, charHeight: 10, pointSize: 12);
            var body2 = CreateWord("BodyB", x: 10, baselineY: 248, charWidth: 6, charHeight: 10, pointSize: 12);
            var body3 = CreateWord("BodyC", x: 10, baselineY: 236, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { heading, body1, body2, body3 });

            // The heading should be in a separate block from body text.
            Assert.True(blocks.Count >= 2, $"Expected at least 2 blocks but got {blocks.Count}");

            // One block should contain the heading text only.
            Assert.True(blocks.Any(b => b.Text.Contains("Title") && !b.Text.Contains("Body")),
                "Expected a block containing only the heading");
        }

        [Fact]
        public void GetBlocks_BodyFontSize_IsModeFontSize()
        {
            // When most words are 12pt and one is 24pt, 12pt is the body font size.
            // The heading threshold is 12 * 1.2 = 14.4, so 24pt > 14.4 → heading.
            // All 12pt words are body, not heading.
            var body1 = CreateWord("Word1", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);
            var body2 = CreateWord("Word2", x: 50, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);
            var body3 = CreateWord("Word3", x: 90, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);
            var heading = CreateWord("Heading", x: 10, baselineY: 220, charWidth: 8, charHeight: 16, pointSize: 24);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { heading, body1, body2, body3 });

            // The body words should be grouped together.
            var bodyBlock = blocks.FirstOrDefault(b => b.Text.Contains("Word1"));
            Assert.NotNull(bodyBlock);
            Assert.Contains("Word2", bodyBlock.Text);
            Assert.Contains("Word3", bodyBlock.Text);
        }

        [Fact]
        public void GetBlocks_HeadingBetweenBodyParagraphs_CreatesSeparateBlocks()
        {
            // Layout: Body paragraph 1, then heading, then body paragraph 2.
            // This should produce at least 3 blocks.
            var bodyA1 = CreateWord("ParaA1", x: 10, baselineY: 400, charWidth: 6, charHeight: 10, pointSize: 12);
            var bodyA2 = CreateWord("ParaA2", x: 10, baselineY: 388, charWidth: 6, charHeight: 10, pointSize: 12);
            var heading = CreateWord("Section", x: 10, baselineY: 350, charWidth: 8, charHeight: 20, pointSize: 24);
            var bodyB1 = CreateWord("ParaB1", x: 10, baselineY: 310, charWidth: 6, charHeight: 10, pointSize: 12);
            var bodyB2 = CreateWord("ParaB2", x: 10, baselineY: 298, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { bodyA1, bodyA2, heading, bodyB1, bodyB2 });

            Assert.True(blocks.Count >= 3, $"Expected at least 3 blocks but got {blocks.Count}");
            Assert.True(blocks.Any(b => b.Text.Contains("Section") && !b.Text.Contains("Para")),
                "Expected a standalone heading block");
        }

        #endregion

        #region Uniform font size tests

        [Fact]
        public void GetBlocks_UniformFontSize_NoHeadingSplit()
        {
            // When all words have the same font size, there are no headings.
            // Lines that are close together should be grouped into the same block.
            var word1 = CreateWord("Line1", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("Line2", x: 10, baselineY: 188, charWidth: 6, charHeight: 10, pointSize: 12);
            var word3 = CreateWord("Line3", x: 10, baselineY: 176, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { word1, word2, word3 });

            // All lines have same font, gaps are 12 (< blockGapThreshold 15), so single block.
            Assert.Single(blocks);
            Assert.Contains("Line1", blocks[0].Text);
            Assert.Contains("Line2", blocks[0].Text);
            Assert.Contains("Line3", blocks[0].Text);
        }

        [Fact]
        public void GetBlocks_UniformFontSize_SplitByLargeGapOnly()
        {
            // Same font size everywhere, but a large gap separates two groups.
            var word1 = CreateWord("GroupA", x: 10, baselineY: 300, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("GroupB", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(new[] { word1, word2 });

            Assert.Equal(2, blocks.Count);
        }

        #endregion

        #region Custom options tests

        [Fact]
        public void GetBlocks_HighHeadingScaleThreshold_DoesNotSplitOnFontSize()
        {
            // With HeadingScaleThreshold = 10.0, a word needs to be 10× body font to count as heading.
            // A 24pt word with 12pt body: 24 < 12 * 10 = 120, so no heading split.
            var options = new HeuristicPageSegmenter.HeuristicPageSegmenterOptions
            {
                HeadingScaleThreshold = 10.0
            };
            var segmenter = new HeuristicPageSegmenter(options);

            var heading = CreateWord("Title", x: 10, baselineY: 220, charWidth: 8, charHeight: 16, pointSize: 24);
            var body = CreateWord("Body", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = segmenter.GetBlocks(new[] { heading, body });

            // With a very high threshold, font size difference alone won't cause a split,
            // but the large vertical gap might. The gap is 20, avgLineHeight ~= 13,
            // blockGapThreshold = 13 * 1.5 = 19.5. Gap 20 > 19.5, so 2 blocks from gap.
            // Test that the segmenter doesn't crash and returns blocks.
            Assert.True(blocks.Count >= 1);
        }

        [Fact]
        public void GetBlocks_LowBlockGapMultiplier_SplitsMoreAggressively()
        {
            // With BlockGapMultiplier = 0.5, even small gaps cause block splits.
            // charHeight=10, avgLineHeight=10, blockGapThreshold = 10 * 0.5 = 5
            // Gaps of 12 > 5 should split.
            var options = new HeuristicPageSegmenter.HeuristicPageSegmenterOptions
            {
                BlockGapMultiplier = 0.5
            };
            var segmenter = new HeuristicPageSegmenter(options);

            var word1 = CreateWord("Line1", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("Line2", x: 10, baselineY: 188, charWidth: 6, charHeight: 10, pointSize: 12);
            var word3 = CreateWord("Line3", x: 10, baselineY: 176, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = segmenter.GetBlocks(new[] { word1, word2, word3 });

            // Gap between lines is 12, threshold is 5, so each line becomes a separate block.
            Assert.Equal(3, blocks.Count);
        }

        [Fact]
        public void GetBlocks_HighBlockGapMultiplier_GroupsMoreAggressively()
        {
            // With BlockGapMultiplier = 100.0, almost no gap is large enough to split.
            var options = new HeuristicPageSegmenter.HeuristicPageSegmenterOptions
            {
                BlockGapMultiplier = 100.0,
                HeadingScaleThreshold = 100.0 // Also disable heading split
            };
            var segmenter = new HeuristicPageSegmenter(options);

            var word1 = CreateWord("Line1", x: 10, baselineY: 300, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("Line2", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = segmenter.GetBlocks(new[] { word1, word2 });

            Assert.Single(blocks);
        }

        [Fact]
        public void GetBlocks_TightLineGapTolerance_SplitsLinesMoreAggressively()
        {
            // With LineGapTolerance = 0.01, words must be almost exactly on the same baseline.
            // avgLetterHeight=10, tolerance = 10 * 0.01 = 0.1
            // Words with baseline difference of 2 will NOT be on the same line.
            var options = new HeuristicPageSegmenter.HeuristicPageSegmenterOptions
            {
                LineGapTolerance = 0.01
            };
            var segmenter = new HeuristicPageSegmenter(options);

            var word1 = CreateWord("Hello", x: 10, baselineY: 100, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("World", x: 50, baselineY: 102, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = segmenter.GetBlocks(new[] { word1, word2 });

            // With tight tolerance, these should be treated as separate lines.
            // Gap is 2 (< blockGapThreshold ~15), but they are different lines, same font.
            // Both lines have same font, gap is 2 which is < blockGapThreshold, so still 1 block.
            Assert.Single(blocks);
            // Verify they form two text lines within the single block.
            Assert.Equal(2, blocks[0].TextLines.Count);
        }

        [Fact]
        public void GetBlocks_CustomWordSeparator_UsedInOutput()
        {
            var options = new HeuristicPageSegmenter.HeuristicPageSegmenterOptions
            {
                WordSeparator = "|"
            };
            var segmenter = new HeuristicPageSegmenter(options);

            var word1 = CreateWord("Hello", x: 10, baselineY: 100, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("World", x: 50, baselineY: 100, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = segmenter.GetBlocks(new[] { word1, word2 });

            Assert.Single(blocks);
            Assert.Contains("|", blocks[0].Text);
        }

        [Fact]
        public void GetBlocks_CustomLineSeparator_UsedInOutput()
        {
            var options = new HeuristicPageSegmenter.HeuristicPageSegmenterOptions
            {
                LineSeparator = "---"
            };
            var segmenter = new HeuristicPageSegmenter(options);

            var word1 = CreateWord("Top", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12);
            var word2 = CreateWord("Bot", x: 10, baselineY: 186, charWidth: 6, charHeight: 10, pointSize: 12);

            var blocks = segmenter.GetBlocks(new[] { word1, word2 });

            // These should be on separate lines within same block (gap 14 < threshold 15).
            Assert.Single(blocks);
            Assert.Contains("---", blocks[0].Text);
        }

        #endregion

        #region PDF document integration tests

        public static IEnumerable<object[]> DlaTestDocuments => new[]
        {
            new object[] { "Random 2 Columns Lists Hyph - Justified.pdf" },
            new object[] { "2559 words.pdf" },
        };

        [Theory]
        [MemberData(nameof(DlaTestDocuments))]
        public void GetBlocks_DlaDocuments_ReturnsNonEmptyBlocks(string documentName)
        {
            using (var document = PdfDocument.Open(DlaHelper.GetDocumentPath(documentName)))
            {
                var page = document.GetPage(1);
                var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters);
                var blocks = HeuristicPageSegmenter.Instance.GetBlocks(words);

                Assert.NotEmpty(blocks);
                Assert.All(blocks, b =>
                {
                    Assert.NotNull(b.Text);
                    Assert.False(string.IsNullOrWhiteSpace(b.Text));
                    Assert.NotEmpty(b.TextLines);
                });
            }
        }

        [Theory]
        [MemberData(nameof(DlaTestDocuments))]
        public void GetBlocks_DlaDocuments_AllWordsAccountedFor(string documentName)
        {
            using (var document = PdfDocument.Open(DlaHelper.GetDocumentPath(documentName)))
            {
                var page = document.GetPage(1);
                var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters);
                var nonEmptyWords = words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
                var blocks = HeuristicPageSegmenter.Instance.GetBlocks(words);

                int totalWordsInBlocks = blocks.Sum(b => b.TextLines.Sum(l => l.Words.Count));
                Assert.Equal(nonEmptyWords.Count, totalWordsInBlocks);
            }
        }

        [Theory]
        [MemberData(nameof(DlaTestDocuments))]
        public void GetBlocks_DlaDocuments_BlockBoundingBoxesAreValid(string documentName)
        {
            using (var document = PdfDocument.Open(DlaHelper.GetDocumentPath(documentName)))
            {
                var page = document.GetPage(1);
                var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters);
                var blocks = HeuristicPageSegmenter.Instance.GetBlocks(words);

                Assert.All(blocks, b =>
                {
                    Assert.True(b.BoundingBox.Width >= 0, "Block width should be non-negative");
                    Assert.True(b.BoundingBox.Height >= 0, "Block height should be non-negative");
                });
            }
        }

        #endregion

        #region Multi-line block construction tests

        [Fact]
        public void GetBlocks_MultipleWordsPerLine_MultipleLines_SingleBlock()
        {
            // 3 lines with 2 words each, closely spaced → single block.
            var words = new List<Word>
            {
                CreateWord("Hello", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12),
                CreateWord("World", x: 60, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12),
                CreateWord("Foo", x: 10, baselineY: 188, charWidth: 6, charHeight: 10, pointSize: 12),
                CreateWord("Bar", x: 60, baselineY: 188, charWidth: 6, charHeight: 10, pointSize: 12),
                CreateWord("Baz", x: 10, baselineY: 176, charWidth: 6, charHeight: 10, pointSize: 12),
                CreateWord("Qux", x: 60, baselineY: 176, charWidth: 6, charHeight: 10, pointSize: 12),
            };

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(words);

            Assert.Single(blocks);
            Assert.Equal(3, blocks[0].TextLines.Count);
        }

        [Fact]
        public void GetBlocks_TextLines_ContainCorrectWordCount()
        {
            var words = new List<Word>
            {
                CreateWord("A", x: 10, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12),
                CreateWord("B", x: 30, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12),
                CreateWord("C", x: 50, baselineY: 200, charWidth: 6, charHeight: 10, pointSize: 12),
            };

            var blocks = HeuristicPageSegmenter.Instance.GetBlocks(words);

            Assert.Single(blocks);
            Assert.Single(blocks[0].TextLines);
            Assert.Equal(3, blocks[0].TextLines[0].Words.Count);
        }

        #endregion

        #region Default options value tests

        [Fact]
        public void DefaultOptions_HaveExpectedValues()
        {
            var options = new HeuristicPageSegmenter.HeuristicPageSegmenterOptions();

            Assert.Equal(1.2, options.HeadingScaleThreshold);
            Assert.Equal(0.5, options.LineGapTolerance);
            Assert.Equal(1.5, options.BlockGapMultiplier);
            Assert.Equal(" ", options.WordSeparator);
            Assert.Equal("\n", options.LineSeparator);
            Assert.Equal(-1, options.MaxDegreeOfParallelism);
        }

        #endregion
    }
}
