using System.Text.Json.Serialization;
using Api.Data;
using Api.Ingest;
using Api.Ingest.Adapters;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
    client.Timeout = TimeSpan.FromSeconds(60);
    // Arbeitnow's meta.terms asks callers not to abuse the free API; identifying
    // the caller is the minimum courtesy that makes a block reversible.
    client.DefaultRequestHeaders.UserAgent.ParseAdd("EmployMe/0.1 (+https://github.com/nupolovykh/EmployMe)");
});

// Adapters are resolved by sources.adapter_type, so registering one here plus a
// row in `sources` is the whole cost of adding a source.
builder.Services.AddSingleton<IJobSource, GreenhouseJobSource>();
builder.Services.AddSingleton<IJobSource, LeverJobSource>();
builder.Services.AddSingleton<IJobSource, JobicyJobSource>();
builder.Services.AddSingleton<IJobSource, ArbeitnowJobSource>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IngestService>();

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

app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health");

app.MapControllers();

app.Run();
