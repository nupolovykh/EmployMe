namespace Api.Models;

public enum ApplicationStatus
{
    Viewed,
    Applied,
    Interview,
    Rejected,
    Offer,
}

public class Application
{
    public int Id { get; set; }

    public int VacancyId { get; set; }
    public Vacancy? Vacancy { get; set; }

    public ApplicationStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset StatusChangedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
