using backend.Data.Entities;

namespace backend.Data;

public interface IDocRepository
{
    Task SaveDocumentAsync(DocumentEntity doc, CancellationToken ct);
    Task<DocumentEntity?> GetDocumentWithChunksAsync(string docId, CancellationToken ct);
    Task<List<DocumentEntity>> ListDocumentsAsync(CancellationToken ct, int take = 25);
}