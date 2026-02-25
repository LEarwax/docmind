namespace backend.Data.Entities;

public class DocumentEntity
{
    public string Id { get; set; } = default!;       // docId (Guid string "N")
    public string FileName { get; set; } = default!;
    public string FullText { get; set; } = default!;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<ChunkEntity> Chunks { get; set; } = new();
}