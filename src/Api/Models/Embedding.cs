using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Api.Models;

public enum EmbeddingSubject
{
    Vacancy,
    Cv,
}

public class Embedding
{
    public int Id { get; set; }

    public EmbeddingSubject Subject { get; set; }

    public int? VacancyId { get; set; }
    public Vacancy? Vacancy { get; set; }

    public required string Model { get; set; }

    [Column(TypeName = "vector(1024)")]
    public required Vector Vector { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
