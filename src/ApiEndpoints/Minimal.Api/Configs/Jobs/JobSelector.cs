namespace Minimal.Api.Configs.Jobs;

/// <summary>
///     The job the process was launched to run, selected from its command-line arguments alone (DRK-1255 §6 R1) —
///     no environment check and no configuration value ever participates in this decision.
/// </summary>
/// <param name="RequestedJobName">
///     The first non-option argument, or <c>null</c> when the arguments carried none (R2).
/// </param>
/// <param name="IsRecognized">
///     Whether <paramref name="RequestedJobName" /> matches one of <paramref name="KnownJobNames" />
///     (case-insensitive). Meaningless when <see cref="HasJobName" /> is <c>false</c>.
/// </param>
/// <param name="KnownJobNames">Every job name the registry recognises, for the R3 failure message.</param>
internal sealed record JobSelection(string? RequestedJobName, bool IsRecognized, IReadOnlyList<string> KnownJobNames)
{
    /// <summary>True when a non-option argument was present at all — recognized or not (R3).</summary>
    public bool HasJobName => RequestedJobName is not null;
}

/// <summary>
///     Selects the job name from process arguments per R2: the first argument that is not an option (an option
///     begins with '-'). A "--key value" pair's value belongs to that option and is never itself a job-name
///     candidate — only a bare non-option argument can be one.
/// </summary>
internal static class JobSelector
{
    public static JobSelection Select(IReadOnlyList<string> args, IReadOnlyCollection<string> knownJobNames)
    {
        throw new NotImplementedException();
    }
}
