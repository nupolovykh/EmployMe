using Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Api.Tests.Support;

/// <summary>
/// A real Postgres for the integration tests (EM-23). Two ways to get one:
/// <list type="bullet">
/// <item><c>EMPLOYME_TEST_POSTGRES</c> — an admin connection string to a server
/// that already exists. This is what the Dev Container and CI use: both run the
/// compose <c>db</c> service and neither has a Docker socket, so Testcontainers
/// cannot start anything there.</item>
/// <item>Otherwise Testcontainers starts <c>pgvector/pgvector:pg18</c>, the same
/// image the compose file pins.</item>
/// </list>
/// Either way every test gets its own database, created from the migrations —
/// so the seeded source and registry rows are exactly the ones production has —
/// and dropped afterwards. Nothing here ever touches the <c>employme</c> dev
/// database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string _adminConnectionString = "";

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("EMPLOYME_TEST_POSTGRES");

        if (!string.IsNullOrWhiteSpace(external))
        {
            _adminConnectionString = external;
            return;
        }

        try
        {
            _container = new PostgreSqlBuilder("pgvector/pgvector:pg18").Build();
            await _container.StartAsync();
            _adminConnectionString = _container.GetConnectionString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "No Postgres for integration tests: EMPLOYME_TEST_POSTGRES is unset and Testcontainers "
                + "could not start one (no Docker?). Inside the Dev Container the variable is set by "
                + "docker-compose.yml; elsewhere point it at any Postgres with pgvector.", ex);
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>A migrated, seeded, empty-of-vacancies database of its own.</summary>
    public async Task<TestDatabase> CreateDatabaseAsync()
    {
        var name = "employme_test_" + Guid.NewGuid().ToString("N")[..12];

        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = name }.ToString();
        var database = new TestDatabase(connectionString, () => DropAsync(name));

        await using var db = database.CreateContext();
        await db.Database.MigrateAsync();

        return database;
    }

    private async Task DropAsync(string name)
    {
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

public sealed class TestDatabase(string connectionString, Func<Task> drop) : IAsyncDisposable
{
    public string ConnectionString { get; } = connectionString;

    public AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString, o => o.UseVector())
            .Options);

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await drop();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
