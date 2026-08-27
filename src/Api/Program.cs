using Api.Data;
using Api.Ingest;
using Api.Ingest.Adapters;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        o => o.UseVector()));

builder.Services.Configure<IngestOptions>(builder.Configuration.GetSection(IngestOptions.SectionName));
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

// Frontend and API deploy as separate Railway services on separate domains —
// no shared origin like the Vite dev-server proxy gives locally.
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

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
