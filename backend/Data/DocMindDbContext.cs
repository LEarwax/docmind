using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class DocMindDbContext : DbContext
{
    public DocMindDbContext(DbContextOptions<DocMindDbContext> options) : base(options) { }

    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
    public DbSet<ChunkEntity> Chunks => Set<ChunkEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentEntity>()
            .HasKey(d => d.Id);

        modelBuilder.Entity<ChunkEntity>()
            .HasIndex(c => new { c.DocumentId, c.ChunkIndex })
            .IsUnique();

        modelBuilder.Entity<DocumentEntity>()
            .HasMany(d => d.Chunks)
            .WithOne(c => c.Document)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}