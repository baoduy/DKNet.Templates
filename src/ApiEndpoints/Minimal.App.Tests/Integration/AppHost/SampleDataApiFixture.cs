using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minimal.App.TestSupport;
using Minimal.Infra.Extensions;
using Testcontainers.PostgreSql;

namespace Minimal.App.Tests.Integration.AppHost;

/// <summary>
/// Boots the real app (real Postgres, real data-owner query filter, real in-memory message bus) against a
/// migrated database — needed for assertions <see cref="SampleDataGeneratorBoundsTests"/> and
/// <see cref="SampleDataGeneratorRetentionTests"/> don't require: whether a generated row is *visible*
/// through <c>DataOwnerAuthQuery</c>'s filter, and whether generation triggers a domain notification.
/// Deliberately not <see cref="TestApiFactoryBase"/>: that base always swaps in EF Core InMemory, but
/// <see cref="Minimal.AppHost.SampleData.SampleDataGenerator"/> writes over a real Npgsql connection
/// string, so the host under test must point at the same real Postgres container. Mirrors
/// <see cref="Minimal.App.Tests.Integration.ManualSample.V1.InfraMigrationSeedingTests"/>'s construction.
/// </summary>
public sealed class SampleDataApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private WebApplicationFactory<Minimal.Api.Program>? _factory;

    public TestLogCapture LogCapture { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Ephemeral containers can reuse a torn-down container's host port; clear Npgsql's
        // connection pools so a fresh container is never handed a stale pooled physical connection.
        Npgsql.NpgsqlConnection.ClearAllPools();
        await InfraMigration.MigrateDb(ConnectionString);

        _factory = new WebApplicationFactory<Minimal.Api.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.AddProvider(LogCapture));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppDb"] = ConnectionString,
                ["FeatureManagement:RunDbMigrationWhenAppStart"] = "false", // already migrated above
                ["FeatureManagement:RequireAuthorization"] = "false",
                ["FeatureManagement:EnableSwagger"] = "false",
                ["FeatureManagement:EnableAzureAppConfig"] = "false"
            }));
        });
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateClient() => _factory!.CreateClient();

    public IServiceScope CreateScope() => _factory!.Services.CreateScope();
}
