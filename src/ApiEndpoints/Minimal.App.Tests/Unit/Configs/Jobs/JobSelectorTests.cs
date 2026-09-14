using Minimal.Api.Configs.Jobs;

namespace Minimal.App.Tests.Unit.Configs.Jobs;

/// <summary>
/// Drives <see cref="JobSelector" /> directly — no host, no database — against DRK-1255 §6 R2/R3 and the §3
/// edge-case table (an option's value, a job name in the wrong case, an empty-string argument, two job names, a
/// job name after an option).
/// </summary>
public class JobSelectorTests
{
    private static readonly string[] KnownJobs = ["migration"];

    [Fact]
    public void Select_NoArguments_HasNoJobName()
    {
        var result = JobSelector.Select([], KnownJobs);

        result.HasJobName.ShouldBeFalse();
    }

    [Fact]
    public void Select_OnlyAnOptionAndItsValue_HasNoJobName()
    {
        // "--urls http://127.0.0.1:0" — the value belongs to --urls, it is never itself a job-name candidate.
        var result = JobSelector.Select(["--urls", "http://127.0.0.1:0"], KnownJobs);

        result.HasJobName.ShouldBeFalse();
    }

    [Fact]
    public void Select_KnownJobName_IsRecognized()
    {
        var result = JobSelector.Select(["migration"], KnownJobs);

        result.RequestedJobName.ShouldBe("migration");
        result.IsRecognized.ShouldBeTrue();
    }

    [Fact]
    public void Select_KnownJobNameInADifferentCase_IsRecognized()
    {
        var result = JobSelector.Select(["MIGRATION"], KnownJobs);

        result.IsRecognized.ShouldBeTrue();
    }

    [Fact]
    public void Select_UnrecognizedJobName_ReportsItAndTheKnownJobs()
    {
        var result = JobSelector.Select(["migrations"], KnownJobs);

        result.HasJobName.ShouldBeTrue();
        result.IsRecognized.ShouldBeFalse();
        result.KnownJobNames.ShouldBe(KnownJobs);
    }

    [Fact]
    public void Select_EmptyStringArgument_IsAnUnrecognizedJobName()
    {
        // An empty string does not begin with '-', so R2 makes it a non-option argument — a job name that
        // happens to be empty, not "no job name".
        var result = JobSelector.Select([""], KnownJobs);

        result.HasJobName.ShouldBeTrue();
        result.IsRecognized.ShouldBeFalse();
    }

    [Fact]
    public void Select_TwoNonOptionArguments_TakesOnlyTheFirst()
    {
        var result = JobSelector.Select(["migration", "extra"], KnownJobs);

        result.RequestedJobName.ShouldBe("migration");
        result.IsRecognized.ShouldBeTrue();
    }

    [Fact]
    public void Select_JobNameAfterAnOptionAndItsValue_IsRecognized()
    {
        // "--urls http://127.0.0.1:0 migration"
        var result = JobSelector.Select(["--urls", "http://127.0.0.1:0", "migration"], KnownJobs);

        result.RequestedJobName.ShouldBe("migration");
        result.IsRecognized.ShouldBeTrue();
    }
}
