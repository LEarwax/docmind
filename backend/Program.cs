var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/ping", () => "backend alive");

app.MapPost("/upload", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Expected form");

    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    if (file == null)
        return Results.BadRequest("No file");

    // Save uploaded file locally
    Directory.CreateDirectory("uploads");
    var safeName = Path.GetFileName(file.FileName);
    var savedPath = Path.Combine("uploads", $"{Guid.NewGuid()}_{safeName}");

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

    // Store in memory
    var docId = Guid.NewGuid().ToString("N");
    DocPipeline.Docs[docId] = new StoredDoc(safeName, text, chunks);

    return Results.Ok(new
    {
        message = "uploaded",
        docId,
        filename = safeName,
        charCount = text.Length,
        chunkCount = chunks.Count
    });
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