using System.Xml.Linq;

namespace Minimal.App.Tests.Architecture.TemplateRepo;

/// <summary>
/// DRK-1296 §3: guards that the template package ships none of
/// <c>.github</c>, <c>.claude</c>, <c>.claude-plugin</c>, <c>docs</c>, <c>.specify</c>, <c>.vscode</c>,
/// and that every <c>&lt;file src&gt;</c> path the nuspec declares actually resolves on disk — so a
/// stale packaging path fails this suite instead of a release.
/// </summary>
public class TemplatePackageContentTests
{
    private static string SrcDir => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    private static string RepoRoot => Path.GetFullPath(Path.Combine(SrcDir, ".."));

    private static readonly string[] DroppedDirectories =
        [".github", ".claude", ".claude-plugin", "docs", ".specify", ".vscode"];

    private static string[] NuspecFileSrcValues() =>
        XDocument.Load(Path.Combine(SrcDir, "DKNet.Minimal.Template.nuspec"))
            .Descendants().Where(e => e.Name.LocalName == "file")
            .Select(e => e.Attribute("src")!.Value)
            .ToArray();

    private static string[] NuspecPackagingPaths() =>
        XDocument.Load(Path.Combine(SrcDir, "DKNet.Minimal.Template.nuspec"))
            .Descendants().Where(e => e.Name.LocalName == "file")
            .SelectMany(e => new[] { e.Attribute("src")?.Value, e.Attribute("target")?.Value })
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .ToArray();

    private static string[] CsprojPackagingPaths() =>
        XDocument.Load(Path.Combine(SrcDir, "DKNet.Minimal.Template.csproj"))
            .Descendants().Where(e => e.Name.LocalName == "Content")
            .SelectMany(e => new[] { e.Attribute("Include")?.Value, e.Attribute("PackagePath")?.Value })
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .ToArray();

    /// <summary>R1: a glob's literal prefix — the path segments before the first one containing '*' —
    /// must exist as a file or directory relative to the nuspec's own directory (<see cref="SrcDir"/>).</summary>
    private static bool ResolvesToExistingPath(string src)
    {
        var segments = src.Replace('\\', Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var literalSegments = segments.TakeWhile(s => !s.Contains('*')).ToArray();
        if (literalSegments.Length == 0) return false;

        var resolved = Path.GetFullPath(Path.Combine(literalSegments.Prepend(SrcDir).ToArray()));
        return File.Exists(resolved) || Directory.Exists(resolved);
    }

    private static bool HasPathSegment(string path, string segment) =>
        path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Any(s => s.Equals(segment, StringComparison.Ordinal));

    private static bool ReferencesDirectoryPrefix(string line, string dir)
    {
        var token = dir + "/";
        var index = 0;
        while ((index = line.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            if (index == 0 || !char.IsLetterOrDigit(line[index - 1])) return true;
            index += token.Length;
        }
        return false;
    }

    // ── @new — every declared packaging path resolves (R1) ──────────────────────────────

    [Fact]
    public void Nuspec_EveryFileSrc_ResolvesToAnExistingFileOrDirectory()
    {
        var srcValues = NuspecFileSrcValues();
        srcValues.ShouldNotBeEmpty();

        var offenders = srcValues.Where(src => !ResolvesToExistingPath(src)).ToArray();

        offenders.ShouldBeEmpty(
            "<file src> value(s) that do not resolve to an existing file or directory under src/: "
            + string.Join(", ", offenders));
    }

    // ── @new — neither packaging file references a dropped directory (R2) ───────────────

    public static IEnumerable<object[]> DroppedDirectoryExamples()
    {
        string[] nuspecDirs = [".github", ".claude", ".claude-plugin", "docs", ".specify", ".vscode"];
        string[] csprojDirs = [".github", ".claude", ".claude-plugin", ".specify", ".vscode"];

        foreach (var dir in nuspecDirs) yield return ["src/DKNet.Minimal.Template.nuspec", dir];
        foreach (var dir in csprojDirs) yield return ["src/DKNet.Minimal.Template.csproj", dir];
    }

    [Theory]
    [MemberData(nameof(DroppedDirectoryExamples))]
    public void PackagingFile_NeverReferencesADroppedDirectory(string relativeFilePath, string droppedDirectory)
    {
        var paths = relativeFilePath.EndsWith(".nuspec", StringComparison.Ordinal)
            ? NuspecPackagingPaths()
            : CsprojPackagingPaths();

        var offenders = paths.Where(p => HasPathSegment(p, droppedDirectory)).ToArray();

        offenders.ShouldBeEmpty(
            $"{relativeFilePath} still references dropped directory '{droppedDirectory}' via: "
            + string.Join(", ", offenders));
    }

    // ── @new — the shipped AGENTS.md links to nothing the package dropped (R3) ──────────

    [Fact]
    public void AgentsMd_ReferencesNoDroppedDirectory()
    {
        var path = Path.Combine(RepoRoot, "AGENTS.md");
        File.Exists(path).ShouldBeTrue();

        var lines = File.ReadAllLines(path);
        var offenders = Enumerable.Range(0, lines.Length)
            .SelectMany(i => DroppedDirectories
                .Where(dir => ReferencesDirectoryPrefix(lines[i], dir))
                .Select(dir => $"line {i + 1} references '{dir}/': {lines[i].Trim()}"))
            .ToArray();

        offenders.ShouldBeEmpty("AGENTS.md references dropped director"
            + (offenders.Length == 1 ? "y" : "ies") + " at: " + string.Join(" | ", offenders));
    }

    // ── @existing — the keeps stay kept (R4) ─────────────────────────────────────────────

    [Fact]
    public void Nuspec_StillDeclaresEverythingOutsideTheSixDirectories()
    {
        var srcValues = NuspecFileSrcValues();

        string[] expectedKeeps =
        [
            "..\\README.md",
            ".template.config\\**\\*",
            "global.json",
            "Directory.Packages.props",
            "coverage.runsettings",
            "DKNet.Templates.sln",
            "..\\AGENTS.md",
            "ApiEndpoints\\**\\*",
        ];

        var missing = expectedKeeps.Except(srcValues).ToArray();
        missing.ShouldBeEmpty("expected keep(s) missing from nuspec <file src>: " + string.Join(", ", missing));
    }
}
