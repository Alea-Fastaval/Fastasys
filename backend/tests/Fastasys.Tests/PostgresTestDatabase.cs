using System.Runtime.CompilerServices;
using Fastasys.ApiService.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fastasys.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FASTASYS_TEST_POSTGRES")))
            Skip = "Set FASTASYS_TEST_POSTGRES to run PostgreSQL migration, constraint, and concurrency tests";
    }
}

/// <summary>Creates and drops only a generated database, never the database in FASTASYS_TEST_POSTGRES.</summary>
internal sealed class PostgresTestDatabase(string connectionString, string name) : IAsyncDisposable
{
    public DbContextOptions<InfosysDbContext> Options { get; } = new DbContextOptionsBuilder<InfosysDbContext>()
        .UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { Database = name, Pooling = false }.ConnectionString).Options;

    public static async Task<PostgresTestDatabase> CreateAsync(CancellationToken ct)
    {
        var connectionString = Environment.GetEnvironmentVariable("FASTASYS_TEST_POSTGRES")!;
        var name = $"fastasys_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync(ct);
        await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
        await create.ExecuteNonQueryAsync(ct);
        return new PostgresTestDatabase(connectionString, name);
    }

    public async ValueTask DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync(CancellationToken.None);
        // The identifier is generated in CreateAsync, never taken from user input.
        await using var drop = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
