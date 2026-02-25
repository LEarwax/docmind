using backend.Models;
using backend.Services;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<OpenAIEmbeddingsClient>();
builder.Services.AddHttpClient<OpenAIResponsesClient>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<DocMindDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("DocMind");
    opt.UseSqlite(cs);
});

builder.Services.AddScoped<IDocRepository, SqliteDocRepository>();

// OpenAPI (optional)
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocMindDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/ping", () => "backend alive");

app.MapPost("/upload", async (
    HttpRequest request,
    OpenAIEmbeddingsClient embeddingsClient,
    IDocRepository repo,
    CancellationToken ct) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Expected form");

    var form = await request.ReadFormAsync(ct);
    var file = form.Files["file"];
    if (file == null)
        return Results.BadRequest("No file");

    // Save uploaded file locally (optional but fine for now)
    Directory.CreateDirectory("uploads");
    var safeName = Path.GetFileName(file.FileName);
    var savedPath = Path.Combine("uploads", $"{Guid.NewGuid()}_{safeName}");

    await using (var stream = File.Create(savedPath))
        await file.CopyToAsync(stream, ct);

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

    // Chunk text
    var chunks = DocPipeline.ChunkText(text);

    // Generate embeddings
    var vectors = new List<float[]>(chunks.Count);
    foreach (var c in chunks)
    {
        var vec = await embeddingsClient.EmbedAsync(c, ct);
        vectors.Add(vec);
    }

    // Create document entity
    var docId = Guid.NewGuid().ToString("N");

    var doc = new DocumentEntity
    {
        Id = docId,
        FileName = safeName,
        FullText = text,
        CreatedUtc = DateTime.UtcNow,
        Chunks = chunks.Select((chunkText, idx) => new ChunkEntity
        {
            DocumentId = docId,
            ChunkIndex = idx,
            Text = chunkText,
            EmbeddingJson = JsonSerializer.Serialize(vectors[idx])
        }).ToList()
    };

    // Save to SQLite
    await repo.SaveDocumentAsync(doc, ct);

    return Results.Ok(new
    {
        message = "uploaded",
        docId,
        filename = safeName,
        charCount = text.Length,
        chunkCount = chunks.Count
    });
});


app.MapPost("/docs/{docId}/search", async (
    string docId,
    SearchRequest req,
    OpenAIEmbeddingsClient embeddingsClient,
    IDocRepository repo,
    CancellationToken ct) =>
{
    var doc = await repo.GetDocumentWithChunksAsync(docId, ct);
    if (doc == null) return Results.NotFound("Unknown docId");

    var qVec = await embeddingsClient.EmbedAsync(req.Query, ct);

    var chunks = doc.Chunks.OrderBy(c => c.ChunkIndex).ToList();

    var top = chunks
        .Select(c =>
        {
            var vec = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson)!;
            var score = DocPipeline.CosineSimilarity(qVec, vec);
            return new { c.ChunkIndex, score, c.Text };
        })
        .OrderByDescending(x => x.score)
        .Take(5)
        .Select(x => new { chunkIndex = x.ChunkIndex, score = x.score, text = x.Text });

    return Results.Ok(top);
});

app.MapGet("/docs/{docId}/chunks", async (string docId, IDocRepository repo, CancellationToken ct) =>
{
    var doc = await repo.GetDocumentWithChunksAsync(docId, ct);
    if (doc == null) return Results.NotFound("Unknown docId");

    var ordered = doc.Chunks.OrderBy(c => c.ChunkIndex).ToList();

    return Results.Ok(new
    {
        docId,
        filename = doc.FileName,
        chunkCount = ordered.Count,
        chunks = ordered.Take(50).Select(c => new { index = c.ChunkIndex, text = c.Text })
    });
});

app.MapPost("/docs/{docId}/ask", async (
    string docId,
    AskRequest req,
    OpenAIEmbeddingsClient embeddingsClient,
    OpenAIResponsesClient responsesClient,
    IDocRepository repo,
    CancellationToken ct) =>
{
    var doc = await repo.GetDocumentWithChunksAsync(docId, ct);
    if (doc == null)
        return Results.NotFound("Unknown docId");

    var topK = Math.Clamp(req.TopK, 1, 10);

    // Embed question
    var qVec = await embeddingsClient.EmbedAsync(req.Question, ct);

    // Score chunks from DB (deserialize embeddings)
    var scored = doc.Chunks
        .OrderBy(c => c.ChunkIndex)
        .Select(c =>
        {
            var vec = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson)!;
            var score = DocPipeline.CosineSimilarity(qVec, vec);
            return new { chunkIndex = c.ChunkIndex, score, text = c.Text };
        })
        .OrderByDescending(x => x.score)
        .Take(topK)
        .ToList();

    var context = string.Join("\n\n", scored.Select(s =>
        $"[Chunk {s.chunkIndex}]\n{s.text}"));

    // Generate (grounded)
    var system = """
        You are DocMind, a document-grounded assistant.
        Answer ONLY using the provided context.
        If the answer is not in the context, say: "I don't know based on this document."
        When you use facts, cite chunk numbers like [Chunk 0].
        Be concise.
        """;

            var user = $"""
        Question: {req.Question}

        Context:
        {context}
        """;

    var answer = await responsesClient.GenerateAsync(system, user, ct);

    return Results.Ok(new
    {
        docId,
        question = req.Question,
        answer,
        sources = scored.Select(s => new
        {
            chunkIndex = s.chunkIndex,
            score = s.score,
            preview = s.text.Length <= 240 ? s.text : s.text[..240] + "…"
        })
    });
});

app.MapGet("/docs", async (IDocRepository repo, CancellationToken ct) =>
{
    var docs = await repo.ListDocumentsAsync(ct, take: 25);
    return Results.Ok(docs.Select(d => new {
        id = d.Id,
        filename = d.FileName,
        createdUtc = d.CreatedUtc,
        chunkCount = d.Chunks.Count
    }));
});


app.Run();