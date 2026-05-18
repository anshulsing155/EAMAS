using EAMAS.Core.Enums;
using EAMAS.Core.Services.AI.Providers;

namespace EAMAS.Core.Services.AI
{
    public class AiProviderFactory
    {
        public IAiProvider Create(AiProviderType type, string apiKey, string model, double temperature = 0.3)
        {
            return type switch
            {
                AiProviderType.OpenAI  => new OpenAiProvider(apiKey, model, temperature),
                AiProviderType.Claude  => new ClaudeProvider(apiKey, model, temperature),
                AiProviderType.Gemini  => new GeminiProvider(apiKey, model, temperature),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }

        public static string[] GetModels(AiProviderType type) => type switch
        {
            AiProviderType.OpenAI => new[] { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo" },
            AiProviderType.Claude => new[] { "claude-opus-4-7", "claude-sonnet-4-6", "claude-haiku-4-5-20251001" },
            AiProviderType.Gemini => new[] { "gemini-2.0-flash", "gemini-1.5-pro", "gemini-1.5-flash" },
            _ => Array.Empty<string>()
        };

        public static string GetDefaultModel(AiProviderType type) => type switch
        {
            AiProviderType.OpenAI => "gpt-4o",
            AiProviderType.Claude => "claude-sonnet-4-6",
            AiProviderType.Gemini => "gemini-2.0-flash",
            _ => string.Empty
        };

        public static string GetEmbeddingModel(AiProviderType type) => type switch
        {
            AiProviderType.OpenAI => "text-embedding-3-small",
            AiProviderType.Claude => "voyage-3",          // via Voyage AI, fallback to OpenAI
            AiProviderType.Gemini => "text-embedding-004",
            _ => string.Empty
        };
    }
}
