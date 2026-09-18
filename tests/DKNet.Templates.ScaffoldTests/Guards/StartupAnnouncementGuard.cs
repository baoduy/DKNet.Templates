namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1576: every startup/feature-switch announcement and both database-migration lines must reach
/// the configured logging pipeline through <c>ILogger</c> instead of <c>Console.WriteLine</c>, so an
/// operator can answer "was HSTS on last Tuesday?" from the log sink. Pure source-text checks — no
/// <c>WebApplicationFactory</c>, no log capture: this repo's own <c>CLAUDE.md</c> reserves runtime
/// logging/telemetry behaviour tests out of the shipped suites, so this guard proves only what this
/// change actually owns — the call site, the single R2 message template, and the <c>nameof</c> token.
/// </summary>
internal static class StartupAnnouncementGuard
{
    /// <summary>R2: the one message template, <c>"{Feature} enabled"</c>, with <paramref name="configClassName" />
    /// as the <c>nameof</c> argument — so the logged token matches the <c>MarkConfigAdded</c> key exactly.</summary>
    internal static bool AnnouncesThroughLogger(string source, string configClassName) =>
        source.Contains($"LogInformation(\"{{Feature}} enabled\", nameof({configClassName}))", StringComparison.Ordinal);

    /// <summary>R1: no <c>Console.WriteLine</c> call remains. Deliberately does not match
    /// <c>Console.Error.WriteLineAsync</c> (R5's process-boundary failure reporting, out of scope).</summary>
    internal static bool WritesToConsole(string source) =>
        source.Contains("Console.WriteLine(", StringComparison.Ordinal);

    /// <summary>Non-overlapping occurrences of <paramref name="token" /> in <paramref name="source" />.</summary>
    internal static int CountOccurrences(string source, string token) =>
        token.Length == 0 ? 0 : (source.Length - source.Replace(token, "", StringComparison.Ordinal).Length) / token.Length;

    /// <summary>
    /// Migration start and completion (§3 row 4): both prose lines moved to <c>LogInformation</c>, the job
    /// no longer writes to <c>Console</c>, the signature widened to <c>WebApplicationBuilder</c> (§5), and
    /// the provider it builds to resolve an <see cref="Microsoft.Extensions.Logging.ILoggerFactory" /> is
    /// disposed with <c>await using</c> — the disposal is what flushes the OTel exporter before exit (R3).
    /// </summary>
    internal static bool MigrationAnnouncesStartAndCompletionThroughLogger(string source) =>
        CountOccurrences(source, "LogInformation(") >= 2 &&
        !WritesToConsole(source) &&
        source.Contains("RunAsync(WebApplicationBuilder", StringComparison.Ordinal) &&
        source.Contains("await using", StringComparison.Ordinal) &&
        source.Contains("BuildServiceProvider()", StringComparison.Ordinal);

    /// <summary>R5: the failure path is unchanged by the widened signature — a migration failure still
    /// writes <c>ex.Message</c> to stderr and returns exit code 1.</summary>
    internal static bool MigrationFailureStillReportsOnStandardErrorWithExitCode1(string source) =>
        source.Contains("RunAsync(WebApplicationBuilder", StringComparison.Ordinal) &&
        source.Contains("Console.Error.WriteLineAsync($\"Db migration failed: {ex.Message}\")", StringComparison.Ordinal) &&
        source.Contains("return 1;", StringComparison.Ordinal);
}
