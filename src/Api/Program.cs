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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health");

app.MapControllers();

app.Run();
