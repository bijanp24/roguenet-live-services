using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using RogueNet.Infrastructure.Data;

namespace RogueNet.IntegrationTests.Fixtures;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private const string DefaultConnectionString =
        "Server=localhost,1433;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private readonly string _baseConnectionString =
        Environment.GetEnvironmentVariable("ROGUENET_TEST_SQL_CONNECTION") ?? DefaultConnectionString;

    private string _databaseName = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public RogueNetWebAppFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _databaseName = $"RogueNetTests_{Guid.NewGuid():N}";

        var masterConnectionString = new SqlConnectionStringBuilder(_baseConnectionString)
        {
            InitialCatalog = "master",
        }.ConnectionString;

        await using (var connection = new SqlConnection(masterConnectionString))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await cmd.ExecuteNonQueryAsync();
        }

        ConnectionString = new SqlConnectionStringBuilder(_baseConnectionString)
        {
            InitialCatalog = _databaseName,
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.MigrateAsync();

        Factory = new RogueNetWebAppFactory { ConnectionString = ConnectionString };
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();

        if (string.IsNullOrEmpty(_databaseName))
        {
            return;
        }

        var masterConnectionString = new SqlConnectionStringBuilder(_baseConnectionString)
        {
            InitialCatalog = "master",
        }.ConnectionString;

        try
        {
            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                IF DB_ID('{_databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{_databaseName}];
                END
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup; leaked databases are harmless and easy to clean manually.
        }
    }
}
