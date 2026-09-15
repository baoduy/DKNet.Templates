using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// R1/R2 (DRK-1271): the shipped template solution never references a project outside src/, and
/// every test project in the repository is a member of the root CI solution.
/// </summary>
internal static class CiSolutionGuard
{
    /// <summary>A project is a test project when it references Microsoft.NET.Test.Sdk — the same
    /// package every test-runner csproj in this repo (xUnit or Reqnroll/NUnit) carries and no
    /// non-test project (e.g. Minimal.App.TestSupport) does.</summary>
    internal static bool IsTestProject(XDocument csproj) =>
        csproj.Descendants("PackageReference")
            .Any(e => e.Attribute("Include")?.Value == "Microsoft.NET.Test.Sdk");

    /// <summary>Test-project relative paths (forward-slash, repo-root-relative) whose text is absent
    /// from the CI solution's own file content.</summary>
    internal static IReadOnlyList<string> TestProjectsMissingFromSolution(
        IEnumerable<string> testProjectRelativePaths,
        string ciSolutionContent) =>
        testProjectRelativePaths
            .Where(relative => !ciSolutionContent.Contains(relative, StringComparison.Ordinal))
            .ToList();

    /// <summary>Project paths declared by a .sln's text that escape the solution's own directory via
    /// a ".." segment.</summary>
    internal static IReadOnlyList<string> ProjectPathsEscapingSolutionDirectory(string slnContent) =>
        Regex.Matches(slnContent, "\"([^\"]+\\.csproj)\"")
            .Select(m => m.Groups[1].Value)
            .Where(p => p.Replace('\\', '/').Split('/').Contains(".."))
            .ToList();
}
