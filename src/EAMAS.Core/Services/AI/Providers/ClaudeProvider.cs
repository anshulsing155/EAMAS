using EAMAS.Core.Enums;
using System.Net.Http.Json;
using System.Text.Json;

namespace EAMAS.Core.Services.AI.Providers
{
    public class ClaudeProvider : IAiProvider
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly double _temperature;

        public AiProviderType ProviderType => AiProviderType.Claude;
        public string ModelName => _model;

        public ClaudeProvider(string apiKey, string model, double temperature = 0.3)
        {
            _model = model;
            _temperature = temperature;
            _http = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
            _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _http.Timeout = TimeSpan.FromSeconds(120);
        }

        public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 4000)
        {
            var body = new
            {
                model = _model,
                max_tokens = maxTokens,
                temperature = _temperature,
                system = systemPrompt,
                messages = new[] { new { role = "user", content = userPrompt } }
            };

            var response = await _http.PostAsJsonAsync("messages", body).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            return json.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
        }

        public async Task<string> CompleteWithHistoryAsync(string systemPrompt, List<AiMessage> history, int maxTokens = 4000)
        {
            var messages = history.Select(m => new { role = m.Role, content = m.Content }).ToList();
            var body = new { model = _model, max_tokens = maxTokens, temperature = _temperature, system = systemPrompt, messages };
            var response = await _http.PostAsJsonAsync("messages", body).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            return json.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
        }

        public async Task<float[]> EmbedAsync(string text)
        {
            // Claude doesn't have a native embedding endpoint — use OpenAI-compatible Voyage AI
            // For MVP: return a zero vector and fall back to keyword search
            // Managers using Claude as provider can still do basic RAG via BM25 keyword matching
            await Task.CompletedTask;
            return Array.Empty<float>();
        }
    }
}
