using EAMAS.Core.Enums;

namespace EAMAS.Core.Services.AI
{
    public class AiMessage
    {
        public string Role { get; set; } = string.Empty;    // user | assistant
        public string Content { get; set; } = string.Empty;
    }

    public interface IAiProvider
    {
        AiProviderType ProviderType { get; }
        string ModelName { get; }

        /// <summary>Single-turn completion with a system prompt.</summary>
        Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 4000);

        /// <summary>Multi-turn completion with history.</summary>
        Task<string> CompleteWithHistoryAsync(string systemPrompt, List<AiMessage> history, int maxTokens = 4000);

        /// <summary>Returns an embedding vector for the given text.</summary>
        Task<float[]> EmbedAsync(string text);
    }
}
