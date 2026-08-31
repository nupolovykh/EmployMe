namespace Api.Models;

public class Vacancy
{
    public int Id { get; set; }

    public int SourceId { get; set; }
    public Source? Source { get; set; }

    public required string ExternalId { get; set; }
    public required string Title { get; set; }
    public string? Company { get; set; }
    public required string Url { get; set; }
    public string? Location { get; set; }
    public string? WorkFormat { get; set; }
    public string? Description { get; set; }
    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// Populated only by sources that return the level as a field (EM-59).
    /// Inference from the description is EM-31.
    /// </summary>
    public Seniority Seniority { get; set; } = Seniority.Unknown;
    public DateTimeOffset FetchedAt { get; set; }

    public Application? Application { get; set; }
    public ICollection<Embedding> Embeddings { get; set; } = [];
}
