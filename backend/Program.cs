using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<OpenAIEmbeddingsClient>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// OpenAPI (optional)
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/ping", () => "backend alive");

app.MapPost("/upload", async (HttpRequest request, OpenAIEmbeddingsClient embeddingsClient) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Expected form");

    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    if (file == null)
        return Results.BadRequest("No file");

    // Save uploaded file locally
    var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    Directory.CreateDirectory(uploadsDir);

    var safeName = Path.GetFileName(file.FileName);
    var savedPath = Path.Combine(uploadsDir, $"{Guid.NewGuid()}_{safeName}");

    await using (var stream = File.Create(savedPath))
        await file.CopyToAsync(stream);

    // Extract text
    string text;
    try
    {
        await using var fs = File.OpenRead(savedPath);
        var diskFile = new FormFile(fs, 0, fs.Length, "file", safeName)
        {
            Headers = new HeaderDictionary(),
            ContentType = file.ContentType
        };

        text = await DocPipeline.ExtractTextAsync(diskFile);
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Text extraction failed: {ex.Message}");
    }

    // Chunk
    var chunks = DocPipeline.ChunkText(text);
    if (chunks.Count == 0)
        return Results.BadRequest("No extractable text found.");

    // Embed
    var vectors = new List<float[]>(chunks.Count);
    foreach (var c in chunks)
        vectors.Add(await embeddingsClient.EmbedAsync(c));

    // Store in memory
    var docId = Guid.NewGuid().ToString("N");
    DocPipeline.Docs[docId] = new StoredDoc(safeName, text, chunks, vectors);

    return Results.Ok(new
    {
        message = "uploaded",
        docId,
        filename = safeName,
        charCount = text.Length,
        chunkCount = chunks.Count,
        embeddingCount = vectors.Count
    });
});

app.MapPost("/docs/{docId}/search", async (
    string docId,
    SearchRequest req,
    OpenAIEmbeddingsClient embeddingsClient) =>
{
    if (!DocPipeline.Docs.TryGetValue(docId, out var doc))
        return Results.NotFound("Unknown docId");

    var qVec = await embeddingsClient.EmbedAsync(req.Query);

    var top = doc.Embeddings
        .Select((vec, i) => new { i, score = DocPipeline.CosineSimilarity(qVec, vec) })
        .OrderByDescending(x => x.score)
        .Take(5)
        .Select(x => new { chunkIndex = x.i, score = x.score, text = doc.Chunks[x.i] })
        .ToList();

    return Results.Ok(top);
});

app.MapGet("/docs/{docId}/chunks", (string docId) =>
{
    if (!DocPipeline.Docs.TryGetValue(docId, out var doc))
        return Results.NotFound("Unknown docId");

    return Results.Ok(new
    {
        docId,
        filename = doc.FileName,
        chunkCount = doc.Chunks.Count,
        chunks = doc.Chunks.Take(50).Select((c, i) => new { index = i, text = c })
    });
});

app.Run();