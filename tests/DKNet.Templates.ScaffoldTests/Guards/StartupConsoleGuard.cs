namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1576 (LOG-001): shipped template source must emit diagnostics through <c>ILogger</c>, not stdout.
/// A <c>Console.WriteLine</c> in the startup path bypasses the logging pipeline <c>Program.cs</c> configures —
/// no level, no category, no structured fields — so it never reaches the consumer's sink (AzureMonitor, OTLP)
/// and a container log rotation drops it. Eight of the shipped calls report whether a security control is on,
/// which is exactly the thing an operator later needs to answer from the log store.
/// <para>
/// This is a Tier-2 baseline rule: the list below holds today's offenders (none since DRK-1576 migrated the
/// last twelve) and the guard only reports files outside it, so any new one fails the build. The allow-list
/// must only ever shrink — deleting an entry as its file is migrated is the point. It is keyed by file rather
/// than by line so unrelated edits do not churn it; a fourteenth call inside an already-listed file is
/// therefore not caught, which is the deliberate cost of that stability.
/// </para>
/// <para>
/// <c>Console.Error.WriteLineAsync</c> is not matched and is not a violation: <c>Program.cs</c> and
/// <c>MigrationJob</c> use it on fatal paths where the logging pipeline may not exist yet.
/// </para>
/// </summary>
internal static class StartupConsoleGuard
{
    /// <summary>Today's offenders, as paths relative to <c>src/</c> with forward slashes. Only ever remove entries.</summary>
    internal static readonly IReadOnlySet<string> KnownViolations = new HashSet<string>(StringComparer.Ordinal)
    {
        // Empty since DRK-1576 migrated the last twelve Configs/*.cs files to ILogger. Only ever remove entries.
    };

    /// <summary>
    /// Files that write to stdout through <c>Console.Write*</c> and are not on <paramref name="baseline"/>.
    /// </summary>
    internal static IReadOnlyList<string> StdoutWritersNotOnBaseline(
        IEnumerable<(string RelativePath, string Content)> files,
        IReadOnlySet<string> baseline) =>
        files
            .Where(f => f.Content.Contains("Console.Write", StringComparison.Ordinal))
            .Select(f => f.RelativePath.Replace('\\', '/'))
            .Where(path => !baseline.Contains(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Every shipped template <c>.cs</c> file under <paramref name="srcDir"/>, as (path relative to
    /// <paramref name="srcDir"/>, content). Excludes bin/obj and the three test projects, which never reach a
    /// consumer's runtime and are free to use the console.
    /// </summary>
    internal static IEnumerable<(string RelativePath, string Content)> ShippedSourceFiles(string srcDir)
    {
        string[] excludedSegments = ["bin", "obj", "Minimal.App.Tests", "Minimal.App.BDDTests", "Minimal.App.TestSupport"];

        return Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(seg =>
                excludedSegments.Contains(seg, StringComparer.OrdinalIgnoreCase)))
            .Select(f => (Path.GetRelativePath(srcDir, f), File.ReadAllText(f)));
    }
}
