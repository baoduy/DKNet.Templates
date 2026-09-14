using System.Text.RegularExpressions;
using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.Launch;

/// <summary>
/// @launch @existing "Application logging is unchanged in both build configurations", @new "A released build run
/// locally shows telemetry on the console", @new "A deployed service exports no telemetry to the console"
/// (DRK-1255 §7, narrowed to launch-mode's console-telemetry surface).
/// </summary>
/// <remarks>
/// Trace export (<c>OpenTelemetry.Exporter.Console</c>'s <c>SimpleActivityExportProcessor</c>) is synchronous per
/// completed request, so the <c>/healthz</c> readiness call itself produces one. Metric export runs on a timer
/// (<c>OTEL_METRIC_EXPORT_INTERVAL</c>, forced low here) rather than per-request. "TraceId: " and "Metric Name: "
/// are the two literal markers <c>OpenTelemetry.Exporter.Console</c> writes for those respectively — confirmed
/// against the installed 1.18.0 package (nothing else in this pipeline emits either string).
/// </remarks>
public sealed class ConsoleTelemetryTests
{
    private static readonly Regex PortPattern = new(@"127\.0\.0\.1:\d+", RegexOptions.Compiled);
    private static readonly Regex BuildConfigurationPattern = new(@"bin[/\\](Debug|Release)[/\\]", RegexOptions.Compiled);
    private static readonly Regex DurationPattern = new(@"\bcompleted after [\d.]+ms\b", RegexOptions.Compiled);

    /// <summary>
    /// Splits into lines, normalizes what varies for reasons R9 does not care about, and sorts before comparing:
    /// (1) lines from <c>InfraSetup.UseNpgsqlWithMigration</c>'s own Debug-only <c>EnableSensitiveDataLogging()</c>
    /// gate (§4/§8: out of scope, KEEP, separately guarded by <c>PackageArchitectureTests</c>) — a real, sanctioned,
    /// permanent Debug/Release difference unrelated to rows 4/5 here — are dropped entirely; (2) the build
    /// configuration name inside the logged content-root path, and the measured health-check duration, are
    /// replaced with placeholders — both vary for reasons that have nothing to do with what is logged; (3) line
    /// ORDER — background start-up tasks (message bus consumer creation, the idempotency-store warning) log
    /// concurrently, so their relative order varies run to run even for two launches of the very same build. R9
    /// asks for the same records and levels, not the same interleaving, so lines are compared as a sorted
    /// multiset rather than a sequence.
    /// </summary>
    private static IReadOnlyList<string> NormalizeForLoggingComparison(string output) =>
        output
            .Split(Environment.NewLine)
            .Where(line => !line.Contains("EntityFrameworkCore.Model.Validation", StringComparison.Ordinal))
            .Where(line => !line.Contains("Sensitive data logging is enabled", StringComparison.Ordinal))
            .Select(line => BuildConfigurationPattern.Replace(line, "bin/CONFIG/"))
            .Select(line => DurationPattern.Replace(line, "completed after Nms"))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();

    private static async Task<string> RunAndCaptureAsync(
        string dllPath,
        string environmentName,
        IReadOnlyDictionary<string, string?> extraOverrides)
    {
        var port = ApiUnderTestBuild.GetFreeTcpPort();
        var overrides = new Dictionary<string, string?>(extraOverrides)
        {
            ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}",
            ["ASPNETCORE_ENVIRONMENT"] = environmentName,
            ["FeatureManagement__RunDbMigrationWhenAppStart"] = "false",
            ["FeatureManagement__EnableSwagger"] = "false",
            ["FeatureManagement__EnableAzureAppConfig"] = "false"
        };

        using var process = RunningApiProcess.Start(dllPath, [], overrides);

        var responded = await process.WaitUntilRespondsAsync(port, "/healthz", TimeSpan.FromSeconds(30));
        responded.ShouldBeTrue($"expected the process to become ready.{Environment.NewLine}{process.Output}");

        // Traces flush synchronously as part of that request; metrics flush on the (forced-low) export interval —
        // give both a moment to reach this process's stdout before reading it.
        await Task.Delay(1500);
        process.Stop();

        return PortPattern.Replace(process.Output, "127.0.0.1:PORT");
    }

    [Fact]
    public async Task TelemetryDisabled_ApplicationLoggingIsUnchangedAcrossBuildConfigurations()
    {
        var logLevelOverrides = new Dictionary<string, string?>
        {
            ["FeatureManagement__EnableOpenTelemetry"] = "false",
            ["Logging__LogLevel__Default"] = "Information",
            ["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information"
        };

        var debugOutput = await RunAndCaptureAsync(ApiUnderTestBuild.BuildAndLocateDll("Debug"), "Testing", logLevelOverrides);
        var releaseOutput = await RunAndCaptureAsync(ApiUnderTestBuild.BuildAndLocateDll("Release"), "Testing", logLevelOverrides);
        var debugLines = NormalizeForLoggingComparison(debugOutput);
        var releaseLines = NormalizeForLoggingComparison(releaseOutput);

        debugLines.ShouldBe(releaseLines,
            "application logging (same destination, same levels — R9) must be the same set of records whether " +
            $"telemetry is disabled under a Debug or a Release build.{Environment.NewLine}--- Debug ---" +
            $"{Environment.NewLine}{debugOutput}{Environment.NewLine}--- Release ---{Environment.NewLine}{releaseOutput}");
    }

    [Fact]
    public async Task DevelopmentEnvironment_ReleaseBuildShowsConsoleTelemetryLikeDebugDoes()
    {
        var telemetryOverrides = new Dictionary<string, string?>
        {
            ["FeatureManagement__EnableOpenTelemetry"] = "true",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",
            ["OTEL_METRIC_EXPORT_INTERVAL"] = "200"
        };

        var output = await RunAndCaptureAsync(ApiUnderTestBuild.BuildAndLocateDll("Release"), "Development", telemetryOverrides);

        output.ShouldContain("TraceId:",
            customMessage: $"expected a Release build under Development to export traces to the console, same as Debug (R8).{Environment.NewLine}{output}");
        output.ShouldContain("Metric Name:",
            customMessage: $"expected a Release build under Development to export metrics to the console, same as Debug (R8).{Environment.NewLine}{output}");
    }

    [Theory]
    [InlineData("Debug")]
    [InlineData("Release")]
    public async Task ProductionEnvironment_ExportsNoConsoleTelemetryInEitherConfiguration(string configuration)
    {
        var telemetryOverrides = new Dictionary<string, string?>
        {
            ["FeatureManagement__EnableOpenTelemetry"] = "true",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",
            ["OTEL_METRIC_EXPORT_INTERVAL"] = "200"
        };

        var output = await RunAndCaptureAsync(ApiUnderTestBuild.BuildAndLocateDll(configuration), "Production", telemetryOverrides);

        output.ShouldNotContain("TraceId:",
            customMessage: $"a Production {configuration} build must never export traces to the console (R8).{Environment.NewLine}{output}");
        output.ShouldNotContain("Metric Name:",
            customMessage: $"a Production {configuration} build must never export metrics to the console (R8).{Environment.NewLine}{output}");
    }
}
