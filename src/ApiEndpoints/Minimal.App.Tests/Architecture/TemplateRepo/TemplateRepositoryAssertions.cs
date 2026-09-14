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
}
