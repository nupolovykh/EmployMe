namespace Api.HhRu;

public class HhRuOptions
{
    public const string SectionName = "HhRu";

    public string BaseUrl { get; set; } = "https://api.hh.ru";

    // Required by hh.ru's API on every request (see docs/general.md "Trebovaniya k zaprosam"):
    // app name + developer contact email, e.g. "EmployMe/1.0 (dev@example.com)".
    public required string UserAgent { get; set; }
}
