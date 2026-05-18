using EAMAS.Core.Enums;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EAMAS.Core.Services.AI.Providers
{
    public class OpenAiProvider : IAiProvider
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly double _temperature;
        private readonly string _apiKey;

        public AiProviderType ProviderType => AiProviderType.OpenAI;
        public string ModelName => _model;

        public OpenAiProvider(string apiKey, string model, double temperature = 0.3)
        {
            _apiKey = apiKey;
            _model = model;
            _temperature = temperature;
            _http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _http.Timeout = TimeSpan.FromSeconds(120);
        }

        public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 4000)
        {
            var body = new
            {
                model = _model,
                temperature = _temperature,
                max_tokens = maxTokens,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            var response = await _http.PostAsJsonAsync("chat/completions", body).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                   ?? string.Empty;
        }

        public async Task<string> CompleteWithHistoryAsync(string systemPrompt, List<AiMessage> history, int maxTokens = 4000)
        {
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };
            messages.AddRange(history.Select(m => new { role = m.Role, content = m.Content }));

            var body = new { model = _model, temperature = _temperature, max_tokens = maxTokens, messages };
            var response = await _http.PostAsJsonAsync("chat/completions", body).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                   ?? string.Empty;
        }

        public async Task<float[]> EmbedAsync(string text)
        {
            var body = new { model = "text-embedding-3-small", input = text };
            var response = await _http.PostAsJsonAsync("embeddings", body).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            var embArray = json.GetProperty("data")[0].GetProperty("embedding");
            return embArray.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
        }
    }
}
