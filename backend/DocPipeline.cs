using UglyToad.PdfPig;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

public static class DocPipeline
{
    public static readonly ConcurrentDictionary<string, StoredDoc> Docs = new();

    public static List<string> ChunkText(string text, int maxChars = 1200, int overlap = 150)
    {
        text = text.Replace("\r\n", "\n");
        var chunks = new List<string>();
        var i = 0;

        while (i < text.Length)
        {
            var end = Math.Min(i + maxChars, text.Length);
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
        var contentType = file.ContentType ?? "";

        if (ext == ".txt" || contentType.StartsWith("text/"))
        {
            using var reader = new StreamReader(file.OpenReadStream());
            return await reader.ReadToEndAsync();
        }

        if (ext == ".pdf" || contentType == "application/pdf")
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
}
