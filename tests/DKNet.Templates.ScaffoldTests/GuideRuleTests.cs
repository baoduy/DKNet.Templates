namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-2110 §5 "Each guide states the AppHost rule": the maintainer guide (root <c>CLAUDE.md</c>, never
/// packed) and the agent guide shipped to every generated service (root <c>AGENTS.md</c>, packed as
/// <c>content/AGENTS.md</c>) both carry the rule sentence verbatim. Repo-only guard: it reads repository
/// files, so it lives here and never in a shipped suite.
/// </summary>
public class GuideRuleTests
{
    private const string AppHostRule =
        "The AppHost is for local runs only: it is excluded from coverage and has no tests.";

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src", ".."));

    [Theory]
    [InlineData("maintainer guide of the template repo", "CLAUDE.md")]
    [InlineData("agent guide shipped to every generated service", "AGENTS.md")]
    public void EachGuide_StatesTheAppHostRule(string guide, string relativePath)
    {
        var path = Path.Combine(RepoRoot, relativePath);
        File.Exists(path).ShouldBeTrue($"{guide} not found at {relativePath}");

        File.ReadAllText(path).ShouldContain(AppHostRule, Case.Sensitive, $"{guide} ({relativePath}) does not state the AppHost rule");
    }
}
