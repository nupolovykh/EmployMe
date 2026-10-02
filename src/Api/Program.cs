using System.Text.Json.Serialization;
using Api.Data;
using Api.Health;
using Api.Ingest;
using Api.Ingest.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Pgvector.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Error monitoring (EM-22). The DSN comes from SENTRY_DSN or a Sentry:Dsn
// setting. It is resolved here rather than left to the SDK: the SDK treats a
// null DSN as a configuration error and refuses to start the host, while an
// empty one means "disabled" — and a local run with no account must start.
// Unhandled exceptions reach Sentry through this middleware; everything the
// code logs at Error or above reaches it through the Serilog sink below.
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"] ?? builder.Configuration["SENTRY_DSN"] ?? "";
    options.Environment = builder.Environment.EnvironmentName;
    options.Release = "employme@" + (typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
    // Errors only: performance tracing is not a Phase II question, and the
    // free plan's transaction quota is better left unspent.
    options.TracesSampleRate = 0;
    // Ingest reports are returned over HTTP and the token travels in a header;
    // neither belongs in an event. The header is redacted rather than the
    // whole request dropped, because the path and query are what make an
    // ingest failure diagnosable.
    options.SendDefaultPii = false;
    options.SetBeforeSend((sentryEvent, _) =>
    {
        sentryEvent.Request.Headers.Remove("X-Ingest-Token");
        return sentryEvent;
    });
});

// Structured logging (EM-21). Levels come from the Serilog section of
// appsettings; the sink is chosen here because it is a property of where the
// process runs, not of the configuration file. Development keeps the readable
// console line; anywhere else emits one JSON object per event, so Render's
// log stream can be searched by property — source slug, status code, elapsed
// — rather than grepped for a phrase.
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    if (context.HostingEnvironment.IsDevelopment())
    {
        configuration.WriteTo.Console();
    }
    else
    {
        configuration.WriteTo.Console(new RenderedCompactJsonFormatter());
    }

    // The SDK is initialised by UseSentry above; this only routes log events
    // into it. Information and up become breadcrumbs on the next error, so an
    // ingest failure arrives with the source and the fetch counts leading up
    // to it; Error and up become events of their own.
    configuration.WriteTo.Sentry(sentry =>
    {
        sentry.InitializeSdk = false;
        sentry.MinimumBreadcrumbLevel = LogEventLevel.Information;
        sentry.MinimumEventLevel = LogEventLevel.Error;
    });
});

// Enums travel as their names, not their numbers. The default numeric form is
// what broke EM-59 on the deployed site: the API answered `"seniority": 0` while
// the frontend types the field as a string union and hides the badge with
// `v.seniority !== 'Unknown'`. A number never equals that string, so the guard
// never fired and every card rendered a bare digit. The query direction hid it —
// ASP.NET binds enum *names* from the query string, so `?seniority=Junior`
// worked and looked like proof the whole path was fine.
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        o =>
        {
            o.UseVector();
            // Serverless Postgres (Neon) suspends its compute after five minutes
            // of inactivity and the free plan cannot turn that off, so the first
            // query after an idle spell meets a pooled connection the server has
            // already dropped. Without a retry that surfaces as a failed request
            // to whoever woke the site up; with one it costs the resume latency
            // and succeeds. Harmless on an always-on Postgres, where the
            // transient errors it retries simply do not occur.
            o.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
        }));

// Unset PublicDeployment resolves to "public unless Development", so a
// deployment that forgets the setting keeps the compliance guards on rather
// than silently turning them off.
builder.Services.AddOptions<IngestOptions>()
    .Bind(builder.Configuration.GetSection(IngestOptions.SectionName))
    .PostConfigure<IHostEnvironment>((o, env) => o.PublicDeployment ??= !env.IsDevelopment());
builder.Services.AddHttpClient(IngestHttp.ClientName, client =>
    {
        // Above the pipeline's total timeout below, so the resilience handler
        // is what decides when a request has failed. HttpClient.Timeout wraps
        // the whole handler chain, retries included; set lower, it would cut a
        // retry short and surface as a plain TaskCanceledException that no
        // strategy ever saw.
        client.Timeout = IngestResilience.TotalTimeout + TimeSpan.FromSeconds(5);
        // Arbeitnow's meta.terms asks callers not to abuse the free API; identifying
        // the caller is the minimum courtesy that makes a block reversible.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EmployMe/0.1 (+https://github.com/nupolovykh/EmployMe)");
    })
    .AddStandardResilienceHandler(IngestResilience.Configure)
    // One pipeline per upstream host, not one shared across the client. The
    // circuit breaker is the reason: with a single pipeline, Jobicy answering
    // 503 for a minute would open the circuit for Greenhouse and Lever too,
    // and a scheduled run would skip three healthy sources over one sick one.
    .SelectPipelineByAuthority();

// Adapters are resolved by sources.adapter_type, so registering one here plus a
// row in `sources` is the whole cost of adding a source.
builder.Services.AddSingleton<IJobSource, GreenhouseJobSource>();
builder.Services.AddSingleton<IJobSource, LeverJobSource>();
builder.Services.AddSingleton<IJobSource, JobicyJobSource>();
builder.Services.AddSingleton<IJobSource, ArbeitnowJobSource>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IngestService>();

// Two readiness questions, tagged so the endpoints below can ask them apart:
// can the API reach its database, and is the ingest pipeline succeeding
// against its sources. Liveness asks neither — see the /health mapping.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
    .AddCheck<SourceHealthCheck>("sources", tags: ["ready"]);

// Frontend and API deploy as separate Railway services on separate domains — no
// shared origin like the Vite dev-server proxy gives locally. The frontend's
// origin is named explicitly: this API also exposes a mutating ingest endpoint,
// and AllowAnyOrigin would let any page on the web put requests to it. An unset
// Cors:AllowedOrigins allows no cross-origin caller at all, which breaks the
// deployed frontend loudly instead of loosening the API quietly.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
        }
    }));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseCors();

// /health is liveness and runs no checks at all: it is what Render polls,
// and Neon suspending its compute after five idle minutes must not read as
// the API being dead — a restart would not fix it and would cost the wake-up.
// /health/ready is the one that touches the database and the source counters.
app.MapHealthChecks("/health", HealthResponse.Options(_ => false));
app.MapHealthChecks("/health/ready", HealthResponse.Options(r => r.Tags.Contains("ready")));

app.MapControllers();

app.Run();
