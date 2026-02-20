using System.Collections.Concurrent;
using UglyToad.PdfPig;

namespace backend.Services;

public static class DocPipeline
{
    public static readonly ConcurrentDictionary<string, backend.Models.StoredDoc> Docs = new();

    public static List<string> ChunkText(string text, int maxChars = 1200, int overlap = 150)
    {
        text = text.Replace("\r\n", "\n");
        var chunks = new List<string>();
        int i = 0;

        while (i < text.Length)
        {
            int end = Math.Min(i + maxChars, text.Length);

            // Snap end backward to nearest whitespace to avoid mid-word splits
            if (end < text.Length)
            {
                while (end > i && !char.IsWhiteSpace(text[end]))
                    end--;
            }
            if (end <= i)
                end = Math.Min(i + maxChars, text.Length);

            var chunk = text.Substring(i, end - i).Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);

            if (end == text.Length) break;
            i = Math.Max(0, end - overlap);
        }

        return chunks;
    }

    public static async Task<string> ExtractTextAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (ext == ".txt" || (file.ContentType?.StartsWith("text/") ?? false))
        {
            using var reader = new StreamReader(file.OpenReadStream());
            return await reader.ReadToEndAsync();
        }

        if (ext == ".pdf" || file.ContentType == "application/pdf")
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;

            using var pdf = PdfDocument.Open(ms);
            var sb = new System.Text.StringBuilder();
            foreach (var page in pdf.GetPages())
                sb.AppendLine(page.Text);

            return sb.ToString();
        }

        throw new InvalidOperationException("Unsupported file type. Upload PDF or TXT.");
    }

    public static double CosineSimilarity(float[] v1, float[] v2)
    {
        double dot = 0, mag1 = 0, mag2 = 0;
        for (int i = 0; i < v1.Length; i++)
        {
            dot += v1[i] * v2[i];
            mag1 += v1[i] * v1[i];
            mag2 += v2[i] * v2[i];
        }
        return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }
}