using System.Diagnostics;
using System.Security.Cryptography;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1257 §7 Background: shells out to scaffold, build and test the dknet-minimal template the
/// way <see cref="RepoHygieneTests.RunGit" /> shells out to git.
/// Scaffolding, and running <c>dotnet build</c>/<c>dotnet test</c> against a scaffolded solution, is
/// itself a template-repository-only property — this whole folder is excluded from scaffolded output
/// (DRK-1257 §3 row 2). Until that exclude lands, a copy of this class can end up inside a generated
/// solution's own test run; <see cref="Available" /> guards every test method against that, the same
/// way <c>RepoHygieneTests.NoUserFile_ShouldBeTrackedByGit</c> guards against not being a git work tree.
/// </summary>
public sealed class TemplateScaffoldFixture : IDisposable
{
    private static readonly string SrcDirValue =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

    private readonly string _rootTempDir =
        Path.Combine(Path.GetTempPath(), "dknet-at-" + Guid.NewGuid().ToString("N"));

    private readonly Dictionary<string, string> _scaffoldDirs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _manifestsAfterScaffold = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (int ExitCode, string Output)> _builds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (int ExitCode, string Output)> _tests = new(StringComparer.Ordinal);

    public TemplateScaffoldFixture()
    {
        if (!Available) return;

        // Background: no dknet-minimal template from another source stays installed for the current
        // user (Q0) — a stale NuGet-published DKNet.Minimal.Template with the same identity otherwise
        // shadows the source install and the scenario silently exercises the wrong template.
        Run("dotnet", "new uninstall DKNet.Minimal.Template");

        var install = Run("dotnet", $"new install \"{SrcDirValue}\" --force");
        if (install.ExitCode != 0)
            throw new InvalidOperationException($"dotnet new install \"{SrcDirValue}\" failed:\n{install.Output}");
    }

    /// <summary>
    /// False only when this class is executing from inside an already-scaffolded solution (copied
    /// there because the row-2 exclude glob is not yet in place) rather than from this repository.
    /// </summary>
    public static bool Available { get; } = File.Exists(Path.Combine(SrcDirValue, ".template.config", "template.json"));

    /// <summary>Scaffolds (once per name, cached) and returns the generated solution's root directory.</summary>
    public string ScaffoldDirFor(string? name)
    {
        var key = name ?? "__default__";
        if (_scaffoldDirs.TryGetValue(key, out var existing)) return existing;

        var outputDir = Path.Combine(_rootTempDir, name ?? "MyMinimalApp");
        var arguments = name is null
            ? $"new dknet-minimal -o \"{outputDir}\""
            : $"new dknet-minimal -n \"{name}\" -o \"{outputDir}\"";

        var result = Run("dotnet", arguments);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"dotnet {arguments} failed:\n{result.Output}");

        _scaffoldDirs[key] = outputDir;
        _manifestsAfterScaffold[key] = Manifest(outputDir);
        return outputDir;
    }

    /// <summary>The file→hash manifest captured immediately after scaffolding, before any build.</summary>
    public IReadOnlyDictionary<string, string> ManifestAfterScaffold(string? name)
    {
        ScaffoldDirFor(name);
        return _manifestsAfterScaffold[name ?? "__default__"];
    }

    /// <summary><c>dotnet build</c> of the scaffolded solution (once per name, cached).</summary>
    public (int ExitCode, string Output) BuildFor(string? name)
    {
        var key = name ?? "__default__";
        if (_builds.TryGetValue(key, out var cached)) return cached;

        var dir = ScaffoldDirFor(name);
        var sln = Directory.GetFiles(dir, "*.sln", SearchOption.TopDirectoryOnly).Single();
        var result = Run("dotnet", $"build \"{sln}\" -v quiet");
        _builds[key] = result;
        return result;
    }

    /// <summary>
    /// <c>dotnet test</c> of the scaffolded solution's App.Tests project with the given vstest filter
    /// (once per name+filter, cached). Builds first so a build failure surfaces as a build failure,
    /// not a confusing test-discovery failure.
    /// </summary>
    public (int ExitCode, string Output) TestFor(string? name, string filter)
    {
        var key = (name ?? "__default__") + "::" + filter;
        if (_tests.TryGetValue(key, out var cached)) return cached;

        var build = BuildFor(name);
        if (build.ExitCode != 0) return build;

        var dir = ScaffoldDirFor(name);
        var testCsproj = Directory.GetFiles(dir, "*.App.Tests.csproj", SearchOption.AllDirectories).Single();
        var result = Run("dotnet", $"test \"{testCsproj}\" --filter \"{filter}\" -v quiet");
        _tests[key] = result;
        return result;
    }

    /// <summary>Relative path → SHA-256 hash of every non-build-output file under <paramref name="dir" />.</summary>
    public static IReadOnlyDictionary<string, string> Manifest(string dir) =>
        Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(seg =>
                seg.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(
                f => Path.GetRelativePath(dir, f),
                f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))),
                StringComparer.Ordinal);

    private static (int ExitCode, string Output) Run(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)!;
        // Read both streams concurrently — reading them sequentially deadlocks once either pipe's
        // buffer fills while the process blocks writing to the other one.
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdOutTask, stdErrTask);
        return (process.ExitCode, stdOutTask.Result + stdErrTask.Result);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootTempDir)) Directory.Delete(_rootTempDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only — a locked file here must never fail the test run.
        }
    }
}
