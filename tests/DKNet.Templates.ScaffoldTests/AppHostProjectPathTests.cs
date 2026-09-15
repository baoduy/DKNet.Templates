using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1258 follow-up: <c>AppHost.cs</c> resolves the Api project via the <c>AddProject(name,
/// projectPath)</c> overload (a plain path string, chosen so a dotted <c>-n</c> value survives
/// template-engine text substitution — see <see cref="AppHostDeployablePurityTests"/> for the
/// sibling purity guard). Unlike the generic <c>AddProject&lt;T&gt;</c> overload it replaced, a
/// path string is not checked by the compiler: a wrong or stale path still builds clean. This
/// guard replaces that lost compile-time guarantee — it asserts every path AppHost.cs orchestrates
/// by also exists as a <c>ProjectReference</c> in <c>Minimal.AppHost.csproj</c>, so the two cannot
/// silently drift apart. Lives in <c>Architecture/</c>, not <c>Architecture/TemplateRepo/</c> — it
/// is a property of the generated service, not of this repository, and must ship with it.
/// </summary>
public class AppHostProjectPathTests
{
    private static readonly Regex AddProjectPathPattern = new(
        @"AddProject\(\s*""[^""]*""\s*,\s*""(?<path>[^""]*\.csproj)""",
        RegexOptions.Compiled);

    private static string SrcDir => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

    private static string AppHostDir => Path.Combine(SrcDir, "ApiEndpoints", "Minimal.AppHost");

    [Fact]
    public void EveryAddProjectPath_ResolvesToAnExistingCsproj()
    {
        var appHostCsPath = Path.Combine(AppHostDir, "AppHost.cs");
        File.Exists(appHostCsPath).ShouldBeTrue();
        var content = File.ReadAllText(appHostCsPath);

        var paths = AddProjectPathPattern.Matches(content)
            .Select(m => m.Groups["path"].Value)
            .ToArray();

        paths.ShouldNotBeEmpty("expected at least one AddProject(name, projectPath) call in AppHost.cs");

        var missing = paths
            .Where(p => !File.Exists(Path.GetFullPath(Path.Combine(AppHostDir, p))))
            .ToArray();

        missing.ShouldBeEmpty(
            "AddProject path(s) that do not resolve to an existing .csproj relative to the AppHost " +
            $"project directory: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryAddProjectPath_IsAlsoAProjectReferenceInAppHostCsproj()
    {
        var appHostCsPath = Path.Combine(AppHostDir, "AppHost.cs");
        var content = File.ReadAllText(appHostCsPath);

        var addProjectTargets = AddProjectPathPattern.Matches(content)
            .Select(m => Path.GetFullPath(Path.Combine(AppHostDir, m.Groups["path"].Value)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        addProjectTargets.ShouldNotBeEmpty("expected at least one AddProject(name, projectPath) call in AppHost.cs");

        var appHostCsprojPath = Path.Combine(AppHostDir, "Minimal.AppHost.csproj");
        File.Exists(appHostCsprojPath).ShouldBeTrue();

        var projectReferenceTargets = XDocument.Load(appHostCsprojPath)
            .Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? "")
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => Path.GetFullPath(Path.Combine(AppHostDir, v.Replace('\\', '/'))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphaned = addProjectTargets.Except(projectReferenceTargets, StringComparer.OrdinalIgnoreCase).ToArray();

        orphaned.ShouldBeEmpty(
            "AppHost.cs orchestrates a project via AddProject that Minimal.AppHost.csproj does not also " +
            $"declare as a ProjectReference, so the two can drift apart with no build-time signal: {string.Join(", ", orphaned)}");
    }
}
