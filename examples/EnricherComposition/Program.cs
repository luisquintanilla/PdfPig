// Enricher Composition Example
// Demonstrates composing multiple processors on the MEDI pipeline
using UglyToad.PdfPig.DataIngestion;
using UglyToad.PdfPig.DataIngestion.Processors;

Console.WriteLine("=== Enricher Composition Example ===");
Console.WriteLine("Shows how to compose PdfPig processors with MEDI's IngestionPipeline.");
Console.WriteLine();

// Show patterns - these require actual IChatClient implementations to run
Console.WriteLine("Pattern 1: Table extraction only");
Console.WriteLine("  pipeline.DocumentProcessors.Add(new VisionTableEnricher(chatClient));");
Console.WriteLine();

Console.WriteLine("Pattern 2: OCR fallback only");
Console.WriteLine("  pipeline.DocumentProcessors.Add(new VisionOcrFallback(chatClient));");
Console.WriteLine();

Console.WriteLine("Pattern 3: Full composition");
Console.WriteLine("  pipeline.DocumentProcessors.Add(new VisionTableEnricher(chatClient));");
Console.WriteLine("  pipeline.DocumentProcessors.Add(new VisionOcrFallback(chatClient));");
Console.WriteLine("  pipeline.ChunkProcessors.Add(new ContextualChunkEnricher(chatClient));");
