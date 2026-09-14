using Minimal.Api.Configs.Jobs;

namespace Minimal.App.Tests.Unit.Configs.Jobs;

/// <summary>
/// Covers the option forms <see cref="JobSelectorTests" /> does not: an option that carries its own value
/// ("--key=value") consumes no following argument, so the job name after it is still selected.
/// </summary>
public class JobSelectorOptionValueTests
{
    private static readonly string[] KnownJobs = ["migration"];

    [Fact]
    public void Select_OptionCarryingItsOwnValue_DoesNotSwallowTheJobName()
    {
        var result = JobSelector.Select(["--ConnectionStrings:AppDb=Host=db;Database=app", "migration"], KnownJobs);

        result.RequestedJobName.ShouldBe("migration");
        result.IsRecognized.ShouldBeTrue();
    }

    [Fact]
    public void Select_OnlyAnOptionCarryingItsOwnValue_HasNoJobName()
    {
        var result = JobSelector.Select(["--urls=http://127.0.0.1:0"], KnownJobs);

        result.HasJobName.ShouldBeFalse();
    }

    [Fact]
    public void Select_SeparatedOptionValueThatContainsAnEquals_IsStillTheOptionsValue()
    {
        // The '=' test applies to the option, not to its separated value: "--key" takes the next argument
        // whatever it looks like.
        var result = JobSelector.Select(["--ConnectionStrings:AppDb", "Host=db;Database=app"], KnownJobs);

        result.HasJobName.ShouldBeFalse();
    }
}
