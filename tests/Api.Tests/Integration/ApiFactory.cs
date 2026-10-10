using Api.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

namespace Api.Tests.Integration;

/// <summary>
/// The real Program.cs pipeline over a test database, as a public deployment.
/// The scheduler is off — a tick would try the live sources — and Sentry has
/// no DSN.
/// </summary>
public sealed class ApiFactory(TestDatabase database, string? triggerToken) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:Default", database.ConnectionString);
        builder.UseSetting("Ingest:PublicDeployment", "true");
        builder.UseSetting("Ingest:TriggerToken", triggerToken);
        builder.UseSetting("Ingest:Scheduler:Enabled", "false");
        builder.UseSetting("Sentry:Dsn", "");
    }
}
