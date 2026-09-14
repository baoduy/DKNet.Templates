using Minimal.App.Tests.Integration.Support;
using Testcontainers.PostgreSql;

namespace Minimal.App.Tests.Integration.Launch;

/// <summary>
/// @launch @new "Start-up migration is honoured in every build configuration" — proven against both build
/// configurations, not just whichever one this test project itself happens to be compiled as (DRK-1255 §7, §9
/// Q1: this builds and launches each configuration explicitly rather than assuming one).
/// </summary>
public sealed class StartupMigrationBothConfigurationsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Theory]
    [InlineData("Debug")]
    [InlineData("Release")]
    public async Task StartupMigration_MigratesAndThenServes(string configuration)
    {
        var dllPath = ApiUnderTestBuild.BuildAndLocateDll(configuration);
        var port = ApiUnderTestBuild.GetFreeTcpPort();

        var overrides = new Dictionary<string, string?>
        {
            ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}",
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["ConnectionStrings__AppDb"] = _postgres.GetConnectionString(),
            ["FeatureManagement__RunDbMigrationWhenAppStart"] = "true",
            ["FeatureManagement__RequireAuthorization"] = "false",
            ["FeatureManagement__EnableSwagger"] = "false",
            ["FeatureManagement__EnableAzureAppConfig"] = "false"
        };

        using var process = RunningApiProcess.Start(dllPath, [], overrides);

        var responded = await process.WaitUntilRespondsAsync(port, "/healthz", TimeSpan.FromSeconds(60));
        responded.ShouldBeTrue(
            $"expected the {configuration} build to migrate and go on to serve within 60s.{Environment.NewLine}{process.Output}");

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        using var response = await client.GetAsync("/v1/purchase-orders?pageIndex=1&pageSize=1");

        response.IsSuccessStatusCode.ShouldBeTrue(
            $"a successful response here proves the {configuration} build actually migrated the schema before " +
            $"serving, not merely that Kestrel came up.{Environment.NewLine}{process.Output}");
    }
}
