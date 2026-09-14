using System.Diagnostics;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// Drives the `dotnet new` engine as a child process: uninstalls any published copy of the
/// template, installs the working tree's copy, scaffolds one solution from it, and exposes the
/// generated directory to every test in the class. Runs once per test class (xUnit class
/// fixture lifetime), not once per test.
/// </summary>
public sealed class ScaffoldFixture : IDisposable
{
    private const string TemplateIdentity = "DKNet.Minimal.Template";
    private const string TemplateShortName = "dknet-minimal";
    private const string GeneratedName = "Contoso";

    public string RepoRoot { get; }
    public string SrcDir { get; }
    public string GeneratedDir { get; }

    public ScaffoldFixture()
    {
        // AppContext.BaseDirectory is .../tests/DKNet.Templates.ScaffoldTests/bin/<config>/<tfm>/
        // 5 levels up reaches the repo root, the same "../../../../.." shape the repo's own
        // architecture tests use from one level deeper under src/ApiEndpoints/*.
        RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        SrcDir = Path.Combine(RepoRoot, "src");

        GeneratedDir = Path.Combine(Path.GetTempPath(), $"dknet-scaffold-{Guid.NewGuid():N}");
        Directory.CreateDirectory(GeneratedDir);

        // R4: tolerate "not installed" (non-zero exit) — a stale published package shadows the
        // working-tree install below, so this best-effort removal must not fail the fixture.
        RunDotNet("new", "uninstall", TemplateIdentity);

        // --force: a prior run (or another worktree checked out from the same path) may have
        // left this exact path installed already; without it "already installed" is exit 106.
        var install = RunDotNet("new", "install", SrcDir, "--force");
        if (install.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet new install \"{SrcDir}\" --force failed (exit {install.ExitCode}):\n{install.Output}");
        }

        var scaffold = RunDotNet("new", TemplateShortName, "-n", GeneratedName, "-o", GeneratedDir);
        if (scaffold.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet new {TemplateShortName} -n {GeneratedName} -o \"{GeneratedDir}\" failed " +
                $"(exit {scaffold.ExitCode}):\n{scaffold.Output}");
        }
    }

    /// <summary>Reads a file under the generated solution, normalising line endings to "\n".</summary>
    public string ReadGenerated(string relativePath) =>
        Normalize(File.ReadAllText(Path.Combine(GeneratedDir, relativePath)));

    /// <summary>Reads a file under the repository checkout, normalising line endings to "\n".</summary>
    public string ReadRepository(string relativePath) =>
        Normalize(File.ReadAllText(Path.Combine(RepoRoot, relativePath)));

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static (int ExitCode, string Output) RunDotNet(params string[] arguments)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout + stderr);
    }

    public void Dispose()
    {
        // Teardown per R4: uninstall again, tolerate failure the same way setup does.
        RunDotNet("new", "uninstall", TemplateIdentity);

        try
        {
            Directory.Delete(GeneratedDir, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup; a locked file must not fail the test run
        }
        catch (UnauthorizedAccessException)
        {
            // best-effort cleanup; a locked file must not fail the test run
        }
    }
}
