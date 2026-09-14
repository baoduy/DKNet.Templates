using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.Launch;

/// <summary>
/// @launch @new "A mistyped job name fails instead of starting a web server" and the matching Scenario Outline
/// row ("migrations" -&gt; exits non-zero naming the recognised jobs) — DRK-1255 §7.
/// </summary>
public sealed class UnrecognizedJobNameTests
{
    [Fact]
    public async Task MistypedJobName_ExitsNonZeroNamingKnownJobsAndNeverServes()
    {
        var port = ApiUnderTestBuild.GetFreeTcpPort();
        var overrides = new Dictionary<string, string?>
        {
            ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}",
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["FeatureManagement__RunDbMigrationWhenAppStart"] = "false",
            ["FeatureManagement__EnableSwagger"] = "false",
            ["FeatureManagement__EnableAzureAppConfig"] = "false"
        };

        using var process = RunningApiProcess.Start(ApiUnderTestBuild.CoLocatedDllPath, ["migrations"], overrides);

        // If it wrongly started serving instead of failing, this succeeds — the exit-code/output assertions
        // below are what a correct implementation is actually judged on.
        var startedServing = await process.WaitUntilRespondsAsync(port, "/healthz", TimeSpan.FromSeconds(5));
        var exited = await process.WaitForExitAsync(TimeSpan.FromSeconds(30));

        startedServing.ShouldBeFalse("an unrecognised job name must never begin serving requests (R3).");
        exited.ShouldBeTrue($"expected a non-zero exit within 30s.{Environment.NewLine}{process.Output}");
        process.ExitCode.ShouldNotBe(0);
        process.Output.ShouldContain("migration",
            customMessage: "the failure message must name the recognised jobs.");
    }
}
