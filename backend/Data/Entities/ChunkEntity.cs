namespace backend.Data.Entities;

public class ChunkEntity
{
    public long Id { get; set; }                      // SQLite autoincrement
    public string DocumentId { get; set; } = default!;
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = default!;

    // Store embedding as JSON string for simplicity (upgrade later to blob/pgvector)
    public string EmbeddingJson { get; set; } = default!;

    public DocumentEntity Document { get; set; } = default!;
}