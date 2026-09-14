using Minimal.App.Tests.Integration.Support;
using Testcontainers.PostgreSql;

namespace Minimal.App.Tests.Integration.Launch;

/// <summary>
/// @launch @new "The migration job migrates the database and exits", "A job run loads nothing it does not
/// need", and the Scenario Outline rows whose outcome is "runs the migration job and exits" (a known job name,
/// a different case, and an option before the job name) — DRK-1255 §7.
/// </summary>
public sealed class MigrationJobLaunchTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private Dictionary<string, string?> OverridesWithReachableDb() => new()
    {
        // A job dispatched correctly never reads this — but the CURRENT (unimplemented) app still serves
        // regardless of the "migration" argument, and every test in this class runs concurrently; pinning each
        // its own free port keeps that (real, expected-today) fallback-to-serving from colliding with siblings
        // on Kestrel's shared default port, which would otherwise crash the process for an unrelated reason.
        ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{ApiUnderTestBuild.GetFreeTcpPort()}",
        ["ConnectionStrings__AppDb"] = _postgres.GetConnectionString(),
        ["FeatureManagement__EnableSwagger"] = "false",
        ["FeatureManagement__EnableAzureAppConfig"] = "false"
    };

    [Theory]
    [InlineData("migration")]
    [InlineData("MIGRATION")]
    public async Task MigrationArgument_MigratesTheDatabaseAndExitsZero(string argument)
    {
        using var process = RunningApiProcess.Start(ApiUnderTestBuild.CoLocatedDllPath, [argument], OverridesWithReachableDb());

        var exited = await process.WaitForExitAsync(TimeSpan.FromSeconds(60));

        exited.ShouldBeTrue(
            $"expected the \"{argument}\" job to exit on its own within 60s.{Environment.NewLine}{process.Output}");
        process.ExitCode.ShouldBe(0, process.Output);
    }

    [Fact]
    public async Task AnOptionBeforeTheJobName_StillRunsTheMigrationJob()
    {
        // "--urls http://127.0.0.1:0 migration"
        using var process = RunningApiProcess.Start(
            ApiUnderTestBuild.CoLocatedDllPath,
            ["--urls", "http://127.0.0.1:0", "migration"],
            OverridesWithReachableDb());

        var exited = await process.WaitForExitAsync(TimeSpan.FromSeconds(60));

        exited.ShouldBeTrue($"expected the job to exit on its own within 60s.{Environment.NewLine}{process.Output}");
        process.ExitCode.ShouldBe(0, process.Output);
    }

    [Fact]
    public async Task UnreachableDatabase_FailsVisiblyWithNonZeroExit()
    {
        // A closed loopback port: nothing listens there, so the connection attempt fails rather than hangs.
        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings__AppDb"] =
                $"Host=127.0.0.1;Port={ApiUnderTestBuild.GetFreeTcpPort()};Database=doesnotexist;Username=postgres;Password=postgres;Timeout=5",
            ["FeatureManagement__EnableSwagger"] = "false",
            ["FeatureManagement__EnableAzureAppConfig"] = "false"
        };

        using var process = RunningApiProcess.Start(ApiUnderTestBuild.CoLocatedDllPath, ["migration"], overrides);

        var exited = await process.WaitForExitAsync(TimeSpan.FromSeconds(90));

        exited.ShouldBeTrue($"expected the process to give up and exit within 90s.{Environment.NewLine}{process.Output}");
        process.ExitCode.ShouldNotBe(0);
        process.Output.ShouldNotBeEmpty("the failure must be visible on the process's output (R5).");
    }

    [Fact]
    public async Task MigrationJob_NeverBindsAnHttpListener()
    {
        // The job is never told an ASPNETCORE_URLS, but this pins an explicit one so a listener can only exist
        // here if the job wrongly stood up a full web host (R4) — deterministic either way, not a "nothing was
        // ever asked to bind here" tautology.
        var port = ApiUnderTestBuild.GetFreeTcpPort();
        var overrides = OverridesWithReachableDb();
        overrides["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";

        using var process = RunningApiProcess.Start(ApiUnderTestBuild.CoLocatedDllPath, ["migration"], overrides);

        var respondedOnThatPort = await process.WaitUntilRespondsAsync(port, "/healthz", TimeSpan.FromSeconds(5));

        respondedOnThatPort.ShouldBeFalse(
            $"a job run must never bind an HTTP listener (R4).{Environment.NewLine}{process.Output}");
    }

    [Fact]
    public async Task MigrationJob_NeverOpensAMessageBusConnection()
    {
        // An AzureBus endpoint that cannot be dialed: if the job wrongly wired the full service-bus setup
        // (ServiceConfigs.AddAllAppServices), connecting to it would throw or hang; a job that never touches it
        // exits cleanly regardless of whether the endpoint is real.
        var overrides = OverridesWithReachableDb();
        overrides["FeatureManagement__EnableServiceBus"] = "true";
        overrides["ConnectionStrings__AzureBus"] = "Endpoint=sb://does-not-exist.invalid/;SharedAccessKeyName=x;SharedAccessKey=x";

        using var process = RunningApiProcess.Start(ApiUnderTestBuild.CoLocatedDllPath, ["migration"], overrides);

        // Short on purpose: today's (unimplemented) app instead starts a full host whose broken AzureBus
        // consumer retries in a tight loop for as long as we wait — 20s is ample to prove "did not exit".
        var exited = await process.WaitForExitAsync(TimeSpan.FromSeconds(20));

        exited.ShouldBeTrue(
            "a job that never opens the message bus must still exit within 20s regardless of AzureBus's " +
            $"reachability (R4).{Environment.NewLine}{process.Output}");
        process.ExitCode.ShouldBe(0, process.Output);
    }
}
