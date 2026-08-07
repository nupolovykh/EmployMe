using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Embedding> Embeddings => Set<Embedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Vacancy>()
            .HasIndex(v => new { v.SourceId, v.ExternalId })
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
