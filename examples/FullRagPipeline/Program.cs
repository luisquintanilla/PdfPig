// Full RAG Pipeline Example
// Demonstrates composing PdfPig with MEDI's IngestionPipeline
using UglyToad.PdfPig.DataIngestion;
using UglyToad.PdfPig.DataIngestion.Processors;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

Console.WriteLine("=== Full RAG Pipeline Example ===");
Console.WriteLine("This example demonstrates the complete pipeline composition.");
Console.WriteLine();

// Show the 4 approaches to document understanding
Console.WriteLine("Approach 1: Basic text extraction (no layout)");
Console.WriteLine("  var reader = new PdfPigReader();");
Console.WriteLine();

Console.WriteLine("Approach 2: Heuristic layout detection");
Console.WriteLine("  var reader = new PdfPigReader(segmenter: HeuristicPageSegmenter.Instance);");
Console.WriteLine();

Console.WriteLine("Approach 3: ONNX ML layout detection (requires DLA.Onnx package + model)");
Console.WriteLine("  var reader = new PdfPigReader(segmenter: new OnnxPageSegmenter(new RtDetrLayoutModel(\"model.onnx\")));");
Console.WriteLine();

Console.WriteLine("Approach 4: Full pipeline with LLM enrichment");
Console.WriteLine("  using var pipeline = new IngestionPipeline<string>(reader, chunker, writer)");
Console.WriteLine("  {");
Console.WriteLine("      DocumentProcessors = { new VisionTableEnricher(chatClient) },");
Console.WriteLine("      ChunkProcessors = { new ContextualChunkEnricher(chatClient) }");
Console.WriteLine("  };");
