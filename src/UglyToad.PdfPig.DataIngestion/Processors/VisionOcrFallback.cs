namespace UglyToad.PdfPig.DataIngestion.Processors
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.DataIngestion;

    /// <summary>
    /// Falls back to vision LLM-based OCR for document elements that have minimal or no text content,
    /// such as scanned pages or image-heavy regions.
    /// </summary>
    public class VisionOcrFallback : IngestionDocumentProcessor
    {
        private readonly IChatClient chatClient;

        /// <summary>
        /// Creates a new <see cref="VisionOcrFallback"/>.
        /// </summary>
        /// <param name="chatClient">The chat client used to interact with a vision-capable LLM.</param>
        /// <exception cref="ArgumentNullException"><paramref name="chatClient"/> is <see langword="null"/>.</exception>
        public VisionOcrFallback(IChatClient chatClient)
        {
            this.chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        }

        /// <inheritdoc/>
        public override async Task<IngestionDocument> ProcessAsync(
            IngestionDocument document, CancellationToken cancellationToken = default)
        {
            foreach (var element in document.EnumerateContent())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrWhiteSpace(element.Text))
                {
                    continue;
                }

                var prompt = "You are an OCR engine. Extract all visible text from the following content. " +
                    "Return only the extracted text, preserving the original layout as much as possible.\n\n" +
                    element.GetMarkdown();

                var messages = new[]
                {
                    new ChatMessage(ChatRole.User, prompt)
                };

                var response = await chatClient.GetResponseAsync(
                    messages,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var ocrText = response.Text;

                if (!string.IsNullOrWhiteSpace(ocrText))
                {
                    element.Text = ocrText;
                    element.Metadata["ocr_source"] = "vision_llm";
                }
            }

            return document;
        }
    }
}