using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class SqliteDocRepository : IDocRepository
{
    private readonly DocMindDbContext _db;
    public SqliteDocRepository(DocMindDbContext db) => _db = db;

    public async Task SaveDocumentAsync(DocumentEntity doc, CancellationToken ct)
    {
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<DocumentEntity>> ListDocumentsAsync(CancellationToken ct, int take = 25)
    {
        return await _db.Documents
            .AsNoTracking()
            .Include(d => d.Chunks)
            .OrderByDescending(d => d.CreatedUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<DocumentEntity?> GetDocumentWithChunksAsync(string docId, CancellationToken ct)
        => _db.Documents
              .Include(d => d.Chunks)
              .FirstOrDefaultAsync(d => d.Id == docId, ct);
}