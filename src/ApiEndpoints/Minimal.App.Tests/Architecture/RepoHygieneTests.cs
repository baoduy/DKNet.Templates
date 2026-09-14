using System.Diagnostics;
using Minimal.App.Tests.Architecture.Guards;

namespace Minimal.App.Tests.Architecture;

/// <summary>
/// R6-R8 (DRK-1233): working-tree invariants with no pure-function seam (R6, R7) are asserted directly
/// against the repo; R8's guard also gets synthetic-input coverage here alongside its real-file check.
/// </summary>
public class RepoHygieneTests
{
    private static string SrcDir => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    private static string RepoRoot => Path.GetFullPath(Path.Combine(SrcDir, ".."));

    private static (int ExitCode, string StdOut) RunGit(string arguments)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)!;
        var stdOut = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdOut);
    }

    private static string[] CsprojAndPropsFilesUnderSrc() =>
        Directory.GetFiles(SrcDir, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(SrcDir, "*.props", SearchOption.AllDirectories))
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(seg =>
                seg.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

    [Fact]
    public void NoUserFile_ShouldBeTrackedByGit()
    {
        // Q1: gate on git being available and this being a work tree; never fail for a missing git.
        var (toplevelExit, _) = RunGit("rev-parse --show-toplevel");
        if (toplevelExit != 0) return;

        var (exitCode, stdOut) = RunGit("ls-files -- *.user");
        exitCode.ShouldBe(0);

        var tracked = stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        tracked.ShouldBeEmpty("no *.user file should be tracked by git, found: " + string.Join(", ", tracked));
    }

    [Fact]
    public void EveryCsprojAndPropsFileUnderSrc_ShouldEndWithANewline()
    {
        var files = CsprojAndPropsFilesUnderSrc();
        files.ShouldNotBeEmpty();

        var offenders = files.Where(f =>
        {
            using var stream = File.OpenRead(f);
            if (stream.Length == 0) return false;
            stream.Seek(-1, SeekOrigin.End);
            return stream.ReadByte() != '\n';
        }).Select(Path.GetFileName).ToArray();

        offenders.ShouldBeEmpty("files missing a trailing newline: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ADeclaredUserSecretsIdWithNoGeneratedGuidSymbol_IsReported()
    {
        string[] declaredIds = ["6bd0969a-fec4-4cae-aba2-dc071b6478e4"];
        using var symbolsDoc = JsonDocument.Parse("{}");

        var result = UserSecretsGuard.IdsWithoutGeneratedGuidSymbol(declaredIds, symbolsDoc.RootElement);

        result.ShouldBe(["6bd0969a-fec4-4cae-aba2-dc071b6478e4"]);
    }

    [Fact]
    public void AParameterSymbol_DoesNotSatisfyTheRule()
    {
        string[] declaredIds = ["6bd0969a-fec4-4cae-aba2-dc071b6478e4"];
        using var symbolsDoc = JsonDocument.Parse("""
            {
              "SomeSymbol": {
                "type": "parameter",
                "replaces": "6bd0969a-fec4-4cae-aba2-dc071b6478e4"
              }
            }
            """);

        var result = UserSecretsGuard.IdsWithoutGeneratedGuidSymbol(declaredIds, symbolsDoc.RootElement);

        result.ShouldBe(["6bd0969a-fec4-4cae-aba2-dc071b6478e4"]);
    }

    [Fact]
    public void AGeneratedGuidSymbol_SatisfiesTheRule()
    {
        string[] declaredIds = ["6bd0969a-fec4-4cae-aba2-dc071b6478e4"];
        using var symbolsDoc = JsonDocument.Parse("""
            {
              "SomeSymbol": {
                "type": "generated",
                "generator": "guid",
                "replaces": "6bd0969a-fec4-4cae-aba2-dc071b6478e4"
              }
            }
            """);

        var result = UserSecretsGuard.IdsWithoutGeneratedGuidSymbol(declaredIds, symbolsDoc.RootElement);

        result.ShouldBeEmpty();
    }
}
