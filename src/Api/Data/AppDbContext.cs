using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Embedding> Embeddings => Set<Embedding>();
    public DbSet<RawPosting> RawPostings => Set<RawPosting>();
    public DbSet<TargetCompany> TargetCompanies => Set<TargetCompany>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Source>()
            .HasIndex(s => s.Slug)
            .IsUnique();

        modelBuilder.Entity<Vacancy>()
            .HasIndex(v => new { v.SourceId, v.ExternalId })
            .IsUnique();

        modelBuilder.Entity<RawPosting>()
            .Property(r => r.Payload)
            .HasColumnType("jsonb");

        // Unique, not merely indexed: it is what enforces one row per posting.
        // Without it the retention rule lives only in IngestService and a second
        // writer would silently restore unbounded growth.
        modelBuilder.Entity<RawPosting>()
            .HasIndex(r => new { r.SourceId, r.ExternalId })
            .IsUnique();

        modelBuilder.Entity<TargetCompany>()
            .HasIndex(t => new { t.SourceId, t.BoardToken })
            .IsUnique();

        modelBuilder.Entity<Application>()
            .HasIndex(a => a.VacancyId)
            .IsUnique();

        modelBuilder.Entity<Vacancy>()
            .HasOne(v => v.Application)
            .WithOne(a => a.Vacancy)
            .HasForeignKey<Application>(a => a.VacancyId);
    }
}
