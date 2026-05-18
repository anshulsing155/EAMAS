using EAMAS.Core.Enums;
using System.Net.Http.Json;
using System.Text.Json;

namespace EAMAS.Core.Services.AI.Providers
{
    public class GeminiProvider : IAiProvider
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly double _temperature;
        private readonly string _apiKey;
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

        public AiProviderType ProviderType => AiProviderType.Gemini;
        public string ModelName => _model;

        public GeminiProvider(string apiKey, string model, double temperature = 0.3)
        {
            _apiKey = apiKey;
            _model = model;
            _temperature = temperature;
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(120);
        }

        public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 4000)
        {
            var url = $"{BaseUrl}{_model}:generateContent?key={_apiKey}";
            var body = new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
                generationConfig = new { temperature = _temperature, maxOutputTokens = maxTokens }
            };

            var response = await _http.PostAsJsonAsync(url, body).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            return json.GetProperty("candidates")[0]
                       .GetProperty("content").GetProperty("parts")[0]
                       .GetProperty("text").GetString() ?? string.Empty;
        }

        public async Task<string> CompleteWithHistoryAsync(string systemPrompt, List<AiMessage> history, int maxTokens = 4000)
        {
            var url = $"{BaseUrl}{_model}:generateContent?key={_apiKey}";
            var contents = history.Select(m => new
            {
                role = m.Role == "assistant" ? "model" : "user",
                parts = new[] { new { text = m.Content } }
            }).ToList();

            var body = new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents,
                generationConfig = new { temperature = _temperature, maxOutputTokens = maxTokens }
            };

            var response = await _http.PostAsJsonAsync(url, body).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            return json.GetProperty("candidates")[0]
                       .GetProperty("content").GetProperty("parts")[0]
                       .GetProperty("text").GetString() ?? string.Empty;
        }

        public async Task<float[]> EmbedAsync(string text)
        {
            var url = $"{BaseUrl}text-embedding-004:embedContent?key={_apiKey}";
            var body = new { model = "models/text-embedding-004", content = new { parts = new[] { new { text } } } };
            var response = await _http.PostAsJsonAsync(url, body).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            var values = json.GetProperty("embedding").GetProperty("values");
            return values.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
        }
    }
}
