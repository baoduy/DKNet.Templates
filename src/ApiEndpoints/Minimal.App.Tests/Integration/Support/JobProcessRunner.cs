using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// Launches <c>Minimal.Api.dll</c> as a real child process and hands back a live handle to it. Sibling to
/// <see cref="RealProcessApiFixtureBase" />, which only supports a long-running server that waits for
/// <c>/healthz</c> and must be killed at teardown; this one is the smallest thing that also supports a run
/// expected to finish on its own — a job, not a server — by exposing exit-code/output after that happens.
/// </summary>
public sealed class RunningApiProcess : IDisposable
{
    // A process that never dispatches (today's unimplemented state) can retry a broken dependency (e.g. an
    // unreachable message bus) in a tight loop for the whole timeout window, producing megabytes of identical
    // lines — enough to have crashed a test run's result serialization once already. Keep only the most recent
    // lines: still enough to diagnose a failure, never enough to blow up the run.
    private const int MaxCapturedLines = 2000;

    private readonly Process _process;
    private readonly ConcurrentQueue<string> _output = new();

    private RunningApiProcess(Process process)
    {
        _process = process;
    }

    public string Output => string.Join(Environment.NewLine, _output);

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    public static RunningApiProcess Start(
        string dllPath,
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string?> environmentOverrides)
    {
        File.Exists(dllPath).ShouldBeTrue($"expected {dllPath} to exist.");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(dllPath)
        };
        startInfo.ArgumentList.Add(dllPath);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Same rationale as RealProcessApiFixtureBase: strip hierarchical config keys this test process may have
        // inherited from a concurrently-running fixture, so "no override supplied" is actually true for the child.
        foreach (var key in startInfo.Environment.Keys.Where(k => k.Contains("__", StringComparison.Ordinal)).ToList())
        {
            startInfo.Environment.Remove(key);
        }

        foreach (var (key, value) in environmentOverrides)
        {
            startInfo.Environment[key] = value;
        }

        var running = new RunningApiProcess(new Process { StartInfo = startInfo });
        running._process.OutputDataReceived += (_, e) => running.Capture(e.Data);
        running._process.ErrorDataReceived += (_, e) => running.Capture(e.Data);

        running._process.Start();
        running._process.BeginOutputReadLine();
        running._process.BeginErrorReadLine();
        return running;
    }

    private void Capture(string? line)
    {
        if (line is null)
        {
            return;
        }

        _output.Enqueue(line);
        while (_output.Count > MaxCapturedLines && _output.TryDequeue(out _))
        {
        }
    }

    /// <summary>Waits for the process to exit on its own. Returns <c>false</c> (without killing it) on timeout.</summary>
    public async Task<bool> WaitForExitAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await _process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Polls <paramref name="path" /> on the given loopback port until it answers or the process exits.</summary>
    public async Task<bool> WaitUntilRespondsAsync(int port, string path, TimeSpan timeout)
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                return false;
            }

            try
            {
                using var response = await client.GetAsync(path);
                return true;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(200);
            }
        }

        return false;
    }

    public void Stop()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }
    }

    public void Dispose()
    {
        Stop();
        _process.Dispose();
    }
}

/// <summary>Locates/builds the <c>Minimal.Api</c> output DLL for a specific build configuration.</summary>
public static class ApiUnderTestBuild
{
    /// <summary>
    /// Builds <c>Minimal.Api</c> in <paramref name="configuration" /> and returns the path to its output DLL.
    /// Needed because the DLL already copied next to this test assembly (via its <c>ProjectReference</c>) is
    /// whatever configuration this test project itself was built in — scenarios that must prove behaviour under
    /// a *specific* configuration (Debug vs. Release) cannot rely on that copy.
    /// </summary>
    public static string BuildAndLocateDll(string configuration)
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var csprojPath = Path.Combine(srcDir, "ApiEndpoints", "Minimal.Api", "Minimal.Api.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"expected {csprojPath} to exist.");

        using var build = Process.Start(new ProcessStartInfo("dotnet",
            $"build \"{csprojPath}\" -c {configuration} --nologo -v quiet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        var stdout = build.StandardOutput.ReadToEnd();
        var stderr = build.StandardError.ReadToEnd();
        build.WaitForExit();
        build.ExitCode.ShouldBe(0, $"dotnet build -c {configuration} failed:{Environment.NewLine}{stdout}{stderr}");

        var dllPath = Path.Combine(srcDir, "ApiEndpoints", "Minimal.Api", "bin", configuration, "net10.0", "Minimal.Api.dll");
        File.Exists(dllPath).ShouldBeTrue($"expected {dllPath} to exist after building {configuration}.");
        return dllPath;
    }

    public static string CoLocatedDllPath => Path.Combine(AppContext.BaseDirectory, "Minimal.Api.dll");

    public static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
