using Api.Ingest;
using Api.Ingest.Adapters;
using Api.Models;
using Api.Tests.Support;

namespace Api.Tests.Adapters;

public class JobicyJobSourceTests
{
    private const string Endpoint = "https://jobicy.com/api/v2/remote-jobs?count=100";

    private static JobSourceContext Context() => new(TestSources.Source("jobicy"), []);

    [Fact]
    public async Task Maps_every_job_and_keeps_the_canonical_url()
    {
        var http = new StubHttpClientFactory().Respond(Endpoint, Spikes.Read("jobicy"));
        var adapter = new JobicyJobSource(http);

        var postings = await adapter.FetchAsync(Context(), CancellationToken.None).ToListAsync();

        Assert.Equal(5, postings.Count);
        Assert.All(postings, p =>
        {
            // Display condition from friendlyNotice: the apply link must be
            // the original Jobicy URL, mapped straight through.
            Assert.StartsWith("https://jobicy.com/jobs/", p.Vacancy.Url);
            Assert.Equal("remote", p.Vacancy.WorkFormat);
            Assert.NotNull(p.Vacancy.Description);
            Assert.DoesNotContain("<p>", p.Vacancy.Description);
        });
    }

    [Fact]
    public async Task Seniority_comes_from_jobLevel()
    {
        var http = new StubHttpClientFactory().Respond(Endpoint, Spikes.Read("jobicy"));
        var adapter = new JobicyJobSource(http);

        var postings = await adapter.FetchAsync(Context(), CancellationToken.None).ToListAsync();

        Assert.Equal(Seniority.Senior, postings[0].Vacancy.Seniority);
    }

    [Fact]
    public async Task Only_yearly_salaries_are_carried_over()
    {
        var http = new StubHttpClientFactory().Respond(Endpoint, """
            {"jobs":[
              {"id":1,"jobTitle":"A","url":"https://jobicy.com/jobs/a","salaryPeriod":"yearly","salaryMin":50000,"salaryMax":70000,"salaryCurrency":"USD"},
              {"id":2,"jobTitle":"B","url":"https://jobicy.com/jobs/b","salaryPeriod":"hourly","salaryMin":40,"salaryMax":60,"salaryCurrency":"USD"}
            ]}
            """);
        var adapter = new JobicyJobSource(http);

        var postings = await adapter.FetchAsync(Context(), CancellationToken.None).ToListAsync();

        Assert.Equal((50000, 70000, "USD"), (postings[0].Vacancy.SalaryMin, postings[0].Vacancy.SalaryMax, postings[0].Vacancy.Currency));
        Assert.Equal((null, null, null), (postings[1].Vacancy.SalaryMin, postings[1].Vacancy.SalaryMax, postings[1].Vacancy.Currency));
    }
}
