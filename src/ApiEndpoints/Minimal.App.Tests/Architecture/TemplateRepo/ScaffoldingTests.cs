using System.Text.RegularExpressions;

namespace Minimal.App.Tests.Architecture.TemplateRepo;

/// <summary>
/// DRK-1257 §7 — dknet-minimal scaffolds a solution the developer owns. Every scenario here shells
/// out to the .NET SDK (<see cref="ScaffoldFixture" />), which is itself a template-repository-only
/// property, not something a generated service should carry — hence this class living under
/// <c>Architecture/TemplateRepo/</c> (§3 row 1), the folder §3 row 2 excludes from scaffolded output.
/// Every method starts with the same guard: if this copy is executing from inside an already-scaffolded
/// solution (possible until row 2 lands), it is a no-op, mirroring
/// <c>RepoHygieneTests.NoUserFile_ShouldBeTrackedByGit</c>'s "not a work tree" early return.
/// </summary>
public sealed class ScaffoldingTests(ScaffoldFixture fixture) : IClassFixture<ScaffoldFixture>
{
    /// <summary>
    /// DRK-1257 §7: the two assertions known red in every scaffolded solution because DRK-1263's
    /// scaffold-time DEBUG-conditional stripping removes the only production code they depend on.
    /// This list is closed — a third exclusion is a new finding, not something to add here (§7).
    /// Deliberately not spelled with the literal preprocessor tokens here: the same DRK-1263 engine
    /// defect this comment describes treats an unpaired occurrence of them as a real, unterminated
    /// conditional and deletes the rest of this file at scaffold time.
    /// </summary>
    private const string ExcludingDrk1263 =
        "FullyQualifiedName!~Integration" +
        "&FullyQualifiedName!~FeatureOptionsBindingTests.EveryFeatureOptionsProperty_ShouldBeReadBySomeProductionClass" +
        "&FullyQualifiedName!~PackageArchitectureTests.DebugGatedConfiguration_ShouldHaveDebugConditional";

    private static string SrcDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    #region @existing / @new — scaffold, build, run the filtered suite

    [Fact]
    public void DefaultScaffoldName_StillProducesTodaysOutput()
    {
        if (!ScaffoldFixture.Available) return;

        var build = fixture.BuildFor(null);
        build.ExitCode.ShouldBe(0, build.Output);

        var test = fixture.TestFor(null, ExcludingDrk1263);
        test.ExitCode.ShouldBe(0, test.Output);
    }

    [Fact]
    public void DottedServiceName_ScaffoldsASolutionThatBuildsAndPassesItsTests()
    {
        if (!ScaffoldFixture.Available) return;

        const string name = "DKNet.Accounts";

        var manifestAfterScaffold = fixture.ManifestAfterScaffold(name);
        var dir = fixture.ScaffoldDirFor(name);
        var manifestBeforeBuild = ScaffoldFixture.Manifest(dir);
        manifestBeforeBuild.Count.ShouldBe(manifestAfterScaffold.Count);
        manifestBeforeBuild.ShouldBe(manifestAfterScaffold,
            "no file should have been hand-edited between scaffolding and building");

        var build = fixture.BuildFor(name);
        build.ExitCode.ShouldBe(0, build.Output);

        var test = fixture.TestFor(name, ExcludingDrk1263);
        test.ExitCode.ShouldBe(0, test.Output);
    }

    [Theory]
    [InlineData("DKNet.Accounts")]
    [InlineData("DKNet.Accounts.Ledger")]
    [InlineData("Acme_Billing")]
    public void EveryAcceptedName_ProducesLegalIdentifiers(string name)
    {
        if (!ScaffoldFixture.Available) return;

        var build = fixture.BuildFor(name);
        build.ExitCode.ShouldBe(0, build.Output);
    }

    #endregion

    #region @new — generated output carries only what a service owns

    [Fact]
    public void ScaffoldedArchitectureSuite_AssertsOnlyWhatAServiceOwns()
    {
        if (!ScaffoldFixture.Available) return;

        var dir = fixture.ScaffoldDirFor(null);
        // Excludes Architecture/TemplateRepo/ itself: that folder's own presence in scaffolded output
        // is R4's concern (ScaffoldedOutput_ContainsNoFileUnderArchitectureTemplateRepo below), and its
        // scaffolding-support code legitimately mentions .template.config for unrelated reasons (install,
        // the Available guard) — this scenario is about what a *service* owns, not this AT's own plumbing.
        var architectureSources = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Select(f => f.Replace(Path.DirectorySeparatorChar, '/'))
            .Where(f => f.Contains("/Architecture/") && !f.Contains("/Architecture/TemplateRepo/"))
            .ToArray();
        architectureSources.ShouldNotBeEmpty();

        var templateConfigOffenders = architectureSources
            .Where(f => File.ReadAllText(f).Contains(".template.config", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();
        templateConfigOffenders.ShouldBeEmpty(
            "no generated architecture test may assert the presence of src/.template.config/template.json: " +
            string.Join(", ", templateConfigOffenders));

        var buildYmlOffenders = architectureSources
            .Where(f => File.ReadAllText(f).Contains("build.yml", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();
        buildYmlOffenders.ShouldBeEmpty(
            "no generated architecture test may assert the presence of .github/workflows/build.yml: " +
            string.Join(", ", buildYmlOffenders));
    }

    [Fact]
    public void ScaffoldedOutput_ContainsNoFileUnderArchitectureTemplateRepo()
    {
        if (!ScaffoldFixture.Available) return;

        var dir = fixture.ScaffoldDirFor(null);
        var offenders = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => f.Replace(Path.DirectorySeparatorChar, '/').Contains("/Architecture/TemplateRepo/"))
            .ToArray();

        offenders.ShouldBeEmpty(
            "no file under Architecture/TemplateRepo/ may reach scaffolded output (§5 contract, R4): " +
            string.Join(", ", offenders));
    }

    #endregion

    #region @new / @existing — the template-repository assertions, checked where the templater never ran

    [Fact]
    public void TemplateRepositoryAssertions_StillRunInTheTemplateRepository()
    {
        if (!ScaffoldFixture.Available) return;

        var testCsproj = Path.Combine(SrcDir, "ApiEndpoints", "Minimal.App.Tests", "Minimal.App.Tests.csproj");
        const string filter =
            "FullyQualifiedName~EveryDeclaredUserSecretsId_ShouldHaveAGeneratedGuidSymbol" +
            "|FullyQualifiedName~TemplateJson_TenantIdAndApiAudienceSymbols_ShouldMatchAppSettingsPlaceholders" +
            "|FullyQualifiedName~CiWorkflow_RunsOnPullRequestAndDevPush_SoTheAuditGatesThePipelineNotJustLocalBuilds";

        var result = RunDotnetTest(testCsproj, filter);

        result.ExitCode.ShouldBe(0, result.Output);
        TotalRan(result.Output).ShouldBe(3, result.Output);
    }

    [Fact]
    public void SensitiveDataLoggingGuard_KeepsItsTeethInTheTemplateRepository()
    {
        if (!ScaffoldFixture.Available) return;

        var path = Path.Combine(SrcDir, "ApiEndpoints", "Minimal.App.Tests", "Architecture", "PackageArchitectureTests.cs");
        File.Exists(path).ShouldBeTrue();
        var source = File.ReadAllText(path);

        source.ShouldContain("DebugGatedConfiguration_ShouldHaveDebugConditional");
        source.ShouldContain("\"#if DEBUG\"");
        source.ShouldContain("\"EnableDetailedErrors()\"");
        source.ShouldContain("\"EnableSensitiveDataLogging()\"");
        source.ShouldContain("\"#endif\"");

        var testCsproj = Path.Combine(SrcDir, "ApiEndpoints", "Minimal.App.Tests", "Minimal.App.Tests.csproj");
        var result = RunDotnetTest(testCsproj, "FullyQualifiedName~DebugGatedConfiguration_ShouldHaveDebugConditional");

        result.ExitCode.ShouldBe(0, result.Output);
        TotalRan(result.Output).ShouldBe(1, result.Output);
    }

    [Fact]
    public void SensitiveDataLoggingGuard_IsStillShippedToAGeneratedService()
    {
        if (!ScaffoldFixture.Available) return;

        var dir = fixture.ScaffoldDirFor(null);
        var testSource = Directory.GetFiles(dir, "PackageArchitectureTests.cs", SearchOption.AllDirectories).Single();
        File.ReadAllText(testSource).ShouldContain("DebugGatedConfiguration_ShouldHaveDebugConditional");

        // Structural only, by design (§7): the same DRK-1263 stripping that empties generated
        // InfraSetup.cs also deletes this method's own ShouldContain literals when scaffolded, so its
        // *content* cannot be asserted here — only that it is present and executed.
        var testCsproj = Directory.GetFiles(dir, "*.App.Tests.csproj", SearchOption.AllDirectories).Single();
        var result = RunDotnetTest(testCsproj, "FullyQualifiedName~DebugGatedConfiguration_ShouldHaveDebugConditional");

        TotalRan(result.Output).ShouldBe(1, result.Output);
    }

    #endregion

    private static (int ExitCode, string Output) RunDotnetTest(string csproj, string filter)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(
            "dotnet", $"test \"{csproj}\" --filter \"{filter}\" -v quiet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = System.Diagnostics.Process.Start(startInfo)!;
        // Read both streams concurrently — reading them sequentially deadlocks once either pipe's
        // buffer fills while the process blocks writing to the other one.
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdOutTask, stdErrTask);
        return (process.ExitCode, stdOutTask.Result + stdErrTask.Result);
    }

    private static int TotalRan(string dotnetTestOutput)
    {
        var match = Regex.Match(dotnetTestOutput, @"Total:\s*(\d+)");
        match.Success.ShouldBeTrue("expected a test-run summary line with a Total count: " + dotnetTestOutput);
        return int.Parse(match.Groups[1].Value);
    }
}
