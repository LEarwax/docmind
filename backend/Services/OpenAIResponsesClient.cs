using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace backend.Services;

public class OpenAIResponsesClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OpenAIResponsesClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("Missing OpenAI:ApiKey");
    }

    public async Task<string> GenerateAsync(string system, string user, CancellationToken ct = default)
    {
        var body = new
        {
            model = "gpt-4.1",
            input = new object[]
            {
                new { type = "message", role = "system", content = system },
                new { type = "message", role = "user", content = user }
            }
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        msg.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(msg, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Responses API error ({(int)resp.StatusCode}): {json}");

        using var doc = JsonDocument.Parse(json);

        // Extract output_text parts
        var sb = new StringBuilder();
        if (doc.RootElement.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in content.EnumerateArray())
                    {
                        if (part.TryGetProperty("type", out var type) &&
                            type.GetString() == "output_text" &&
                            part.TryGetProperty("text", out var text))
                        {
                            sb.Append(text.GetString());
                        }
                    }
                }
            }
        }

        var result = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("Responses API returned no text output.");

        return result;
    }
}