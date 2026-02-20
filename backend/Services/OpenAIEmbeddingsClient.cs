using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace backend.Services;

public class OpenAIEmbeddingsClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OpenAIEmbeddingsClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("Missing OpenAI:ApiKey");
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var req = new
        {
            model = "text-embedding-3-small",
            input = text
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        msg.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(msg, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Embeddings API error ({(int)resp.StatusCode}): {json}");

        var parsed = JsonSerializer.Deserialize<EmbeddingsResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var vec = parsed?.Data?.FirstOrDefault()?.Embedding;
        if (vec == null || vec.Length == 0)
            throw new InvalidOperationException("Embeddings API returned empty embedding.");

        return vec;
    }

    private sealed class EmbeddingsResponse
    {
        public List<EmbeddingsData> Data { get; set; } = new();
    }

    private sealed class EmbeddingsData
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}