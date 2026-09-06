namespace Api.Ingest;

/// <summary>
/// A connector for one kind of upstream. Implementations are resolved by
/// <see cref="AdapterType"/> against the <c>sources.adapter_type</c> column, so
/// adding or retiring a source is a row change plus one class — never a change
/// to the ingest pipeline. This is the modelling fix for the hh.ru incident
/// (PLAN.md §01 rule 6).
/// </summary>
public interface IJobSource
{
    /// <summary>Matches <c>sources.adapter_type</c>. See docs/SOURCES.md.</summary>
    string AdapterType { get; }

    IAsyncEnumerable<FetchedPosting> FetchAsync(JobSourceContext context, CancellationToken cancellationToken);
}
