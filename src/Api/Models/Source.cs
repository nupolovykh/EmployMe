namespace Api.Models;

public enum SourceType
{
    HhRu,
    JobsGe,
    International,
}

public class Source
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public SourceType Type { get; set; }
    public string? BaseUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Vacancy> Vacancies { get; set; } = [];
}
