using System.Xml.Linq;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// The template's layer rule is <c>Api → AppServices → Domains ← Infra</c>, with <c>Share</c> readable by every
/// layer (<c>CLAUDE.md</c>, <c>AGENTS.md</c>). The NetArchTest layer rules in <c>Minimal.App.Tests</c> only see
/// types that exist today, and MSBuild only refuses an outward reference when it closes a cycle — so a
/// <c>ProjectReference</c> that points outward without a cycle (e.g. <c>Infra → AppServices</c>) compiles cleanly and
/// lets the next contributor couple infrastructure to application code in every scaffolded service.
/// <para>
/// Tier-2 guard (architecture review DRK-2482): <see cref="KnownViolations" /> is today's baseline and may only
/// shrink. Fixing the layering finding filed from DRK-2482 removes the one entry;
/// <see cref="EveryBaselineEntry_IsStillAViolation" /> fails if an entry goes stale.
/// </para>
/// </summary>
public class ProjectReferenceLayerTests
{
    /// <summary>
    /// The projects each shipped production project may reference. <c>Minimal.Api</c> is the composition root and
    /// <c>Minimal.AppHost</c> the local-run orchestrator, so both may see every inner layer.
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedReferences = new(StringComparer.Ordinal)
    {
        ["Minimal.Share"] = [],
        ["Minimal.Domains"] = ["Minimal.Share"],
        ["Minimal.AppServices"] = ["Minimal.Domains", "Minimal.Share"],
        ["Minimal.Infra"] = ["Minimal.Domains", "Minimal.Share"],
        ["Minimal.Api"] = ["Minimal.AppServices", "Minimal.Infra", "Minimal.Domains", "Minimal.Share"],
        ["Minimal.AppHost"] =
            ["Minimal.Api", "Minimal.AppServices", "Minimal.Infra", "Minimal.Domains", "Minimal.Share"],
        ["Minimal.Client"] = []
    };

    /// <summary>Today's offenders as <c>From -> To</c> (DRK-2482). Only ever remove entries.</summary>
    private static readonly HashSet<string> KnownViolations = new(StringComparer.Ordinal)
    {
        "Minimal.Infra -> Minimal.AppServices"
    };

    private static string ApiEndpointsDir => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src/ApiEndpoints"));

    private static (string Name, string[] References)[] ProductionProjects() =>
    [
        .. Directory.GetFiles(ApiEndpointsDir, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj"))
            .Select(f => (Name: Path.GetFileNameWithoutExtension(f), Csproj: XDocument.Load(f)))
            .Where(p => !p.Csproj.Descendants("PackageReference")
                .Any(r => r.Attribute("Include")?.Value == "Microsoft.NET.Test.Sdk"))
            .Where(p => !p.Name.EndsWith(".TestSupport", StringComparison.Ordinal))
            .Select(p => (p.Name, p.Csproj.Descendants("ProjectReference")
                .Select(r => Path.GetFileNameWithoutExtension(
                    (r.Attribute("Include")?.Value ?? string.Empty).Replace('\\', '/')))
                .ToArray()))
    ];

    private static string[] OutwardReferences() =>
    [
        .. ProductionProjects()
            .SelectMany(p => p.References
                .Where(r => !AllowedReferences.GetValueOrDefault(p.Name, []).Contains(r, StringComparer.Ordinal))
                .Select(r => $"{p.Name} -> {r}"))
            .Order()
    ];

    [Fact]
    public void EveryProductionProjectReference_MustPointInward()
    {
        var offenders = OutwardReferences().Where(e => !KnownViolations.Contains(e)).ToArray();

        offenders.ShouldBeEmpty(
            "a ProjectReference that points outward lets inner layers compile against outer-layer types, so the " +
            "onion (Api -> AppServices -> Domains <- Infra, Share read by all) erodes in every scaffolded service " +
            "without any type-level test noticing until the coupling is already written. Reference the package the " +
            "code actually needs instead of the outer project. Do not add the edge to KnownViolations, which may " +
            "only shrink. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryBaselineEntry_IsStillAViolation()
    {
        var stale = KnownViolations.Except(OutwardReferences(), StringComparer.Ordinal).Order().ToArray();

        stale.ShouldBeEmpty(
            "these references are gone, so delete them from KnownViolations to lock the fix in (DRK-2482): " +
            string.Join(", ", stale));
    }

    [Fact]
    public void Rule_CoversEveryShippedProductionProject()
    {
        // Self-check: a new production project missing from AllowedReferences would be checked against an empty
        // allow-list by accident; a moved folder would make the scan find nothing and pass vacuously.
        var names = ProductionProjects().Select(p => p.Name).Order().ToArray();

        names.ShouldNotBeEmpty("the layering rule must scan src/ApiEndpoints.");
        names.Except(AllowedReferences.Keys, StringComparer.Ordinal).ShouldBeEmpty(
            "every shipped production project needs an explicit entry in AllowedReferences, naming the layers it " +
            "may reference.");
    }
}
