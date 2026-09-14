using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.Launch;

/// <summary>
/// @launch @existing "Launching with no job name serves requests as before", plus the two Scenario Outline rows
/// whose outcome is "serves" — no argument at all, and an option with a value (DRK-1255 §7, §3 edge case: "the
/// argument that is an option rather than a job name").
/// </summary>
public sealed class NoJobNameAndOptionOnlyServesTests
{
    private static Dictionary<string, string?> BaseOverrides() => new()
    {
        ["FeatureManagement__RunDbMigrationWhenAppStart"] = "false",
        ["FeatureManagement__EnableSwagger"] = "false",
        ["FeatureManagement__EnableAzureAppConfig"] = "false"
    };

    [Fact]
    public async Task NoArguments_ServesTheSameAsBaseline()
    {
        var port = ApiUnderTestBuild.GetFreeTcpPort();
        var overrides = BaseOverrides();
        overrides["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        overrides["ASPNETCORE_ENVIRONMENT"] = "Testing";

        using var process = RunningApiProcess.Start(ApiUnderTestBuild.CoLocatedDllPath, [], overrides);

        var responded = await process.WaitUntilRespondsAsync(port, "/healthz", TimeSpan.FromSeconds(30));

        responded.ShouldBeTrue(
            $"expected the service to serve /healthz with no job name.{Environment.NewLine}{process.Output}");
    }

    [Fact]
    public async Task AnOptionWithAValue_Serves()
    {
        // "--urls http://127.0.0.1:0" — the value belongs to --urls; it must never be read as a job name (R2).
        var port = ApiUnderTestBuild.GetFreeTcpPort();
        var overrides = BaseOverrides();
        overrides["ASPNETCORE_ENVIRONMENT"] = "Testing";

        using var process = RunningApiProcess.Start(
            ApiUnderTestBuild.CoLocatedDllPath,
            ["--urls", $"http://127.0.0.1:{port}"],
            overrides);

        var responded = await process.WaitUntilRespondsAsync(port, "/healthz", TimeSpan.FromSeconds(30));

        responded.ShouldBeTrue(
            $"expected \"--urls\"'s value to serve, never to be read as a job name.{Environment.NewLine}{process.Output}");
    }
}
