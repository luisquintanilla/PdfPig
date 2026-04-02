namespace UglyToad.PdfPig.DataIngestion.Processors
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.DataIngestion;

    /// <summary>
    /// Enriches table elements in a document by sending their content
    /// to a vision-capable LLM via <see cref="IChatClient"/> to extract markdown table content.
    /// </summary>
    public class VisionTableEnricher : IngestionDocumentProcessor
    {
        private readonly IChatClient chatClient;

        /// <summary>
        /// Creates a new <see cref="VisionTableEnricher"/>.
        /// </summary>
        /// <param name="chatClient">The chat client used to interact with a vision-capable LLM.</param>
        /// <exception cref="ArgumentNullException"><paramref name="chatClient"/> is <see langword="null"/>.</exception>
        public VisionTableEnricher(IChatClient chatClient)
        {
            this.chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        }

        /// <inheritdoc/>
        public override async Task<IngestionDocument> ProcessAsync(
            IngestionDocument document, CancellationToken cancellationToken = default)
        {
            foreach (var element in document.EnumerateContent())
            {
                if (element is not IngestionDocumentTable table)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var prompt = "Extract the following table content into a well-formatted markdown table. " +
                    "Only output the markdown table, no other text.\n\n" +
                    table.GetMarkdown();

                var messages = new[]
                {
                    new ChatMessage(ChatRole.User, prompt)
                };

                var response = await chatClient.GetResponseAsync(
                    messages,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                table.Metadata["enriched_markdown_table"] = response.Text;
            }

            return document;
        }
    }
}