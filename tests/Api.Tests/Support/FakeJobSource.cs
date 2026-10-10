using System.Runtime.CompilerServices;
using Api.Ingest;

namespace Api.Tests.Support;

/// <summary>An adapter that yields a scripted list, or throws, without any HTTP.</summary>
public sealed class FakeJobSource(string adapterType, Func<IEnumerable<FetchedPosting>> postings) : IJobSource
{
    public string AdapterType { get; } = adapterType;

    public int Calls { get; private set; }

    public static FakeJobSource Returning(string adapterType, int count, string prefix = "job") =>
        new(adapterType, () => Enumerable.Range(1, count).Select(i => Posting($"{prefix}-{i}")));

    public static FakeJobSource Throwing(string adapterType, Exception exception) =>
        new(adapterType, () => throw exception);

    public static FetchedPosting Posting(string externalId, string title = "Developer") =>
        new(
            new NormalizedVacancy
            {
                ExternalId = externalId,
                Title = title,
                Url = $"https://example.test/{externalId}",
                Company = "Example",
                PublishedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            },
            $$"""{"id":"{{externalId}}","title":"{{title}}"}""");

    public async IAsyncEnumerable<FetchedPosting> FetchAsync(
        JobSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Calls++;

        foreach (var posting in postings())
        {
            yield return posting;
        }

        await Task.CompletedTask;
    }
}
