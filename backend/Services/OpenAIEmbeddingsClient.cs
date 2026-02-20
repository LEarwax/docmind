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

    public async Task<List<float[]>> EmbedManyAsync(IReadOnlyList<string> inputs, CancellationToken ct = default)
    {
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        if (inputs.Count == 0) return new List<float[]>();

        // Keep batch sizes reasonable to avoid payload limits; tune later if needed.
        const int batchSize = 96;

        var all = new List<float[]>(inputs.Count);

        for (int start = 0; start < inputs.Count; start += batchSize)
        {
            var slice = inputs.Skip(start).Take(batchSize).ToArray();

            var req = new
            {
                model = "text-embedding-3-small",
                input = slice
            };

            using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
            msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            msg.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(msg, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Embeddings API error ({(int)resp.StatusCode}): {json}");

            var parsed = JsonSerializer.Deserialize<EmbeddingsResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed?.Data == null || parsed.Data.Count == 0)
                throw new InvalidOperationException("Embeddings API returned empty data.");

            // The API returns one embedding per input item, in order.
            foreach (var item in parsed.Data)
            {
                if (item.Embedding == null || item.Embedding.Length == 0)
                    throw new InvalidOperationException("Embeddings API returned an empty embedding vector.");
                all.Add(item.Embedding);
            }
        }

        if (all.Count != inputs.Count)
            throw new InvalidOperationException($"Embedding count mismatch: expected {inputs.Count}, got {all.Count}.");

        return all;
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