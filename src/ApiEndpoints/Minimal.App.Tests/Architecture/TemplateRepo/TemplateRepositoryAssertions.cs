using System.Xml.Linq;
using Minimal.App.Tests.Architecture.Guards;

namespace Minimal.App.Tests.Architecture.TemplateRepo;

/// <summary>
/// DRK-1257 §3 rows 3, 4, 5, 10: assertions that are properties of the template repository itself
/// (its own <c>.template.config/template.json</c>, its own CI workflow, its own docs) rather than
/// anything a generated service owns. Relocated here, unchanged in what each asserts, so a copy
/// excluded from scaffolded output (§3 row 2) never reaches a generated service and never goes
/// missing from this repository's own suite.
/// </summary>
public class TemplateRepositoryAssertions
{
    private static string SrcDir => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    [Fact]
    public void EveryDeclaredUserSecretsId_ShouldHaveAGeneratedGuidSymbol()
    {
        var csprojFiles = Directory.GetFiles(SrcDir, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(seg =>
                seg.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        csprojFiles.ShouldNotBeEmpty();

        var declaredIds = UserSecretsGuard.DeclaredUserSecretsIds(csprojFiles.Select(XDocument.Load));

        var templateJsonPath = Path.Combine(SrcDir, ".template.config", "template.json");
        File.Exists(templateJsonPath).ShouldBeTrue();
        using var templateDoc = JsonDocument.Parse(File.ReadAllText(templateJsonPath));
        var symbols = templateDoc.RootElement.GetProperty("symbols");

        var offenders = UserSecretsGuard.IdsWithoutGeneratedGuidSymbol(declaredIds, symbols);

        offenders.ShouldBeEmpty(
            "UserSecretsId value(s) with no generated-guid template.json symbol: " + string.Join(", ", offenders));
    }

    [Fact]
    public void TemplateJson_TenantIdAndApiAudienceSymbols_ShouldMatchAppSettingsPlaceholders()
    {
        var templateJsonPath = Path.Combine(SrcDir, ".template.config", "template.json");
        File.Exists(templateJsonPath).ShouldBeTrue();

        using var templateDoc = JsonDocument.Parse(File.ReadAllText(templateJsonPath));
        var symbols = templateDoc.RootElement.GetProperty("symbols");

        var tenantIdReplaces = symbols.GetProperty("TenantId").GetProperty("replaces").GetString();
        var apiAudienceReplaces = symbols.GetProperty("ApiAudience").GetProperty("replaces").GetString();

        tenantIdReplaces.ShouldNotBeNullOrWhiteSpace();
        apiAudienceReplaces.ShouldNotBeNullOrWhiteSpace();

        var result = EntraPlaceholderGuard.Check(
            AppSettingsScanner.BearerSectionsInAppSettings(SrcDir), tenantIdReplaces!, apiAudienceReplaces!);

        result.Offenders.ShouldBeEmpty(
            "template.json symbols no longer match shipped Entra config: " + string.Join(", ", result.Offenders));
        result.EntraSectionsChecked.ShouldBeGreaterThanOrEqualTo(1,
            "expected at least one Entra-shaped appsettings*.json Bearer section");
    }

    [Fact]
    public void CiWorkflow_RunsOnPullRequestAndDevPush_SoTheAuditGatesThePipelineNotJustLocalBuilds()
    {
        var path = Path.Combine(SrcDir, "..", ".github", "workflows", "build.yml");
        File.Exists(path).ShouldBeTrue($"{path} should exist so the audit gates CI, not only local builds.");
        var content = File.ReadAllText(path);

        content.ShouldContain("pull_request");
        content.ShouldContain("dev");
        content.ShouldContain("dotnet build");
    }

    [Fact]
    public void ManualVsAutomatedDoc_LayersTheAutomatedSampleGenerates_ListsDomainActions()
    {
        var docPath = Path.GetFullPath(Path.Combine(SrcDir, "..", "docs", "samples", "manual-vs-automated.md"));
        File.Exists(docPath).ShouldBeTrue();

        var content = File.ReadAllText(docPath);
        var sectionStart = content.IndexOf("## Layers the automated sample generates", StringComparison.Ordinal);
        sectionStart.ShouldBeGreaterThanOrEqualTo(0);

        var nextSectionStart = content.IndexOf("\n## ", sectionStart + 1, StringComparison.Ordinal);
        var section = nextSectionStart > 0 ? content[sectionStart..nextSectionStart] : content[sectionStart..];

        // The generated-layers enumeration must list the [CrudAction] domain-action layer.
        section.ShouldContain("[CrudAction]");
    }

    // ── DRK-1271 §3 rows 1, 5, 6 — every test project in the repository actually runs ──────

    private static string RepoRoot => Path.GetFullPath(Path.Combine(SrcDir, ".."));

    private const string ScaffoldTestsRelativePath =
        "tests/DKNet.Templates.ScaffoldTests/DKNet.Templates.ScaffoldTests.csproj";

    /// <summary>DRK-1271 §3 row 1 / §7 "the orphaned scaffold acceptance tests are in the CI solution".</summary>
    [Fact]
    public void CiSolution_IncludesTheScaffoldTestsProject()
    {
        var ciSolutionPath = Path.Combine(RepoRoot, "DKNet.Templates.slnx");
        File.Exists(ciSolutionPath).ShouldBeTrue($"expected a root CI solution at {ciSolutionPath}");

        File.ReadAllText(ciSolutionPath).ShouldContain(ScaffoldTestsRelativePath);
    }

    /// <summary>
    /// DRK-1271 §3 row 5 / §7 "no test project in the repository is left out of the CI solution".
    /// "Is a test project" is <see cref="CiSolutionGuard.IsTestProject"/> — a Microsoft.NET.Test.Sdk
    /// reference — not a name match, so Minimal.App.TestSupport is correctly never required here
    /// (see <see cref="TestSupportProject_IsNotClassifiedAsATestProject"/>).
    /// </summary>
    [Fact]
    public void EveryTestProjectInTheRepository_IsIncludedInTheCiSolution()
    {
        var csprojFiles = Directory.GetFiles(RepoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(seg =>
                seg.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        csprojFiles.ShouldNotBeEmpty();

        var testProjectRelativePaths = csprojFiles
            .Where(f => CiSolutionGuard.IsTestProject(XDocument.Load(f)))
            .Select(f => Path.GetRelativePath(RepoRoot, f).Replace(Path.DirectorySeparatorChar, '/'))
            .ToArray();
        testProjectRelativePaths.ShouldNotBeEmpty();

        var ciSolutionPath = Path.Combine(RepoRoot, "DKNet.Templates.slnx");
        File.Exists(ciSolutionPath).ShouldBeTrue($"expected a root CI solution at {ciSolutionPath}");
        var ciSolutionContent = File.ReadAllText(ciSolutionPath);

        var offenders = CiSolutionGuard.TestProjectsMissingFromSolution(testProjectRelativePaths, ciSolutionContent);

        offenders.ShouldBeEmpty(
            "test project(s) missing from the root CI solution: " + string.Join(", ", offenders));
    }

    /// <summary>DRK-1271 §7 "TestSupport is not mistaken for a test project" — a design invariant of
    /// <see cref="CiSolutionGuard.IsTestProject"/> itself, independent of the CI solution's contents.</summary>
    [Fact]
    public void TestSupportProject_IsNotClassifiedAsATestProject()
    {
        var testSupportCsproj = Path.Combine(
            RepoRoot, "src", "ApiEndpoints", "Minimal.App.TestSupport", "Minimal.App.TestSupport.csproj");
        File.Exists(testSupportCsproj).ShouldBeTrue();

        CiSolutionGuard.IsTestProject(XDocument.Load(testSupportCsproj)).ShouldBeFalse(
            "Minimal.App.TestSupport has no Microsoft.NET.Test.Sdk reference and must not be required " +
            "by the CI-solution-membership guard");
    }

    /// <summary>DRK-1271 §3 row 6 / §7 "the shipped solution never points outside the template payload"
    /// — the executable form of the §2 warning against adding the ScaffoldTests project to
    /// src/DKNet.Templates.sln directly.</summary>
    [Fact]
    public void ShippedTemplateSolution_ReferencesNoProjectOutsideSrc()
    {
        var shippedSolutionPath = Path.Combine(SrcDir, "DKNet.Templates.sln");
        File.Exists(shippedSolutionPath).ShouldBeTrue();

        var offenders = CiSolutionGuard.ProjectPathsEscapingSolutionDirectory(File.ReadAllText(shippedSolutionPath));

        offenders.ShouldBeEmpty(
            "project path(s) in src/DKNet.Templates.sln escape src/: " + string.Join(", ", offenders));
    }

    /// <summary>DRK-1271 §3 row 3 / §7 "CI runs the CI solution".</summary>
    [Fact]
    public void CiWorkflow_RestoreBuildAndTestSteps_TargetTheCiSolution()
    {
        var path = Path.Combine(SrcDir, "..", ".github", "workflows", "build.yml");
        File.Exists(path).ShouldBeTrue();
        var content = File.ReadAllText(path);

        content.ShouldNotContain("src/DKNet.Templates.sln");

        var solutionSteps = content.Split('\n')
            .Where(line => line.Contains("dotnet restore") || line.Contains("dotnet build") || line.Contains("dotnet test"))
            .ToArray();
        solutionSteps.ShouldNotBeEmpty();

        var offenders = solutionSteps.Where(line => !line.Contains("DKNet.Templates.slnx")).ToArray();
        offenders.ShouldBeEmpty(
            "restore/build/test step(s) not targeting DKNet.Templates.slnx: " + string.Join(" | ", offenders));
    }

    /// <summary>DRK-1271 §3 row 4 / §7 "test assemblies do not race over the global template store"
    /// (R4): the two scaffold fixtures both mutate the machine-global `dotnet new` store for
    /// DKNet.Minimal.Template and must never run concurrently.</summary>
    [Fact]
    public void CoverageRunSettings_PinsMaxCpuCountToOne()
    {
        var path = Path.Combine(SrcDir, "coverage.runsettings");
        File.Exists(path).ShouldBeTrue();

        var doc = XDocument.Load(path);
        var maxCpuCount = doc.Root?.Element("RunConfiguration")?.Element("MaxCpuCount")?.Value;

        maxCpuCount.ShouldBe("1",
            "expected RunConfiguration/MaxCpuCount=1 (R4): the two scaffold fixtures both mutate the " +
            "global dotnet-new template store for DKNet.Minimal.Template and must not run concurrently");
    }
}
