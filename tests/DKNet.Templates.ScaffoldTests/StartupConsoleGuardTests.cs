namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1576 (LOG-001): real-tree check plus synthetic-input coverage for <see cref="StartupConsoleGuard"/>.
/// Lives here rather than in <c>Minimal.App.Tests</c> because it is a repo-only guard on the template's own
/// shape — it must never ship inside a consumer's generated solution.
/// </summary>
public class StartupConsoleGuardTests
{
    private static string SrcDir => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

    [Fact]
    public void NoShippedFileOutsideTheBaseline_WritesToStdout()
    {
        var files = StartupConsoleGuard.ShippedSourceFiles(SrcDir).ToArray();
        files.ShouldNotBeEmpty();

        var offenders = StartupConsoleGuard.StdoutWritersNotOnBaseline(
            files, StartupConsoleGuard.KnownViolations);

        offenders.ShouldBeEmpty(
            "shipped template source must report startup state through ILogger so it reaches the consumer's " +
            "configured sink — a Console.WriteLine goes to stdout only, unlevelled and unstructured, and is lost " +
            "on log rotation. New offenders: " + string.Join(", ", offenders) +
            ". Use app.Logger.LogInformation(...) or an injected ILogger<T>; do not add the file to " +
            "StartupConsoleGuard.KnownViolations, which may only shrink.");
    }

    [Fact]
    public void EveryBaselineEntry_StillWritesToStdout()
    {
        // Keeps the allow-list honest: an entry whose file has been migrated (or renamed away) must be deleted,
        // otherwise the list silently grows stale and stops meaning "today's offenders".
        var writers = StartupConsoleGuard.ShippedSourceFiles(SrcDir)
            .Where(f => f.Content.Contains("Console.Write", StringComparison.Ordinal))
            .Select(f => f.RelativePath.Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        var stale = StartupConsoleGuard.KnownViolations.Where(entry => !writers.Contains(entry)).Order().ToArray();

        stale.ShouldBeEmpty(
            "these files no longer write to stdout, so their entries must be deleted from " +
            "StartupConsoleGuard.KnownViolations: " + string.Join(", ", stale));
    }

    [Fact]
    public void AFileOutsideTheBaseline_IsReported()
    {
        var result = StartupConsoleGuard.StdoutWritersNotOnBaseline(
            [("ApiEndpoints/Minimal.Api/Configs/NewConfig.cs", "Console.WriteLine(\"New enabled.\");")],
            StartupConsoleGuard.KnownViolations);

        result.ShouldBe(["ApiEndpoints/Minimal.Api/Configs/NewConfig.cs"]);
    }

    [Fact]
    public void AFileOnTheBaseline_IsNotReported()
    {
        // Synthetic baseline: the real KnownViolations is empty now, so this checks the allow-list mechanics only.
        var baseline = new HashSet<string>(StringComparer.Ordinal) { "ApiEndpoints/Minimal.Api/Configs/CrosConfig.cs" };

        var result = StartupConsoleGuard.StdoutWritersNotOnBaseline(
            [("ApiEndpoints/Minimal.Api/Configs/CrosConfig.cs", "Console.WriteLine(\"CROS enabled.\");")],
            baseline);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void WritingToStandardError_IsNotAViolation()
    {
        // Program.cs and MigrationJob report fatal startup failures on stderr, where no logger is guaranteed
        // to exist yet. That is the one console write the rule deliberately permits.
        var result = StartupConsoleGuard.StdoutWritersNotOnBaseline(
            [("ApiEndpoints/Minimal.Api/Program.cs", "await Console.Error.WriteLineAsync(\"fatal\");")],
            StartupConsoleGuard.KnownViolations);

        result.ShouldBeEmpty();
    }
}
