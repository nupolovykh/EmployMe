using Api.Data;
using Api.HhRu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        o => o.UseVector()));

builder.Services.AddOptions<HhRuOptions>()
    .Bind(builder.Configuration.GetSection(HhRuOptions.SectionName));

builder.Services.AddHttpClient<IHhRuVacancyClient, HhRuVacancyClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<HhRuOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("HH-User-Agent", options.UserAgent);
});

builder.Services.AddScoped<HhRuIngestService>();

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
