namespace UglyToad.Examples
{
    using System;
    using System.Linq;
    using PdfPig;
    using PdfPig.DocumentLayoutAnalysis.PageSegmenter;

    public static class HeuristicLayoutExample
    {
        public static void Run(string filePath)
        {
            using (var document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    var words = page.GetWords().ToList();

                    // Heuristic segmenter
                    var heuristicBlocks = HeuristicPageSegmenter.Instance.GetBlocks(words);
                    Console.WriteLine($"=== Heuristic: {heuristicBlocks.Count} blocks ===");
                    foreach (var block in heuristicBlocks)
                    {
                        Console.WriteLine($"  [{block.BoundingBox}] {block.Text.Substring(0, Math.Min(80, block.Text.Length))}...");
                    }

                    // Compare with XY Cut
                    var xyCutBlocks = RecursiveXYCut.Instance.GetBlocks(words);
                    Console.WriteLine($"\n=== XY Cut: {xyCutBlocks.Count} blocks ===");
                    foreach (var block in xyCutBlocks)
                    {
                        Console.WriteLine($"  [{block.BoundingBox}] {block.Text.Substring(0, Math.Min(80, block.Text.Length))}...");
                    }
                }
            }
        }
    }
}
