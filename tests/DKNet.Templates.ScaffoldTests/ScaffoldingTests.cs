namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1257 §7 — dknet-minimal scaffolds a solution the developer owns. Every scenario here shells
/// out to the .NET SDK (<see cref="TemplateScaffoldFixture" />). This class lives in
/// <c>tests/DKNet.Templates.ScaffoldTests/</c>, never shipped or packed. Every method starts with the
/// same guard: if this copy is executing from inside an already-scaffolded solution, it is a no-op,
/// mirroring <c>RepoHygieneTests.NoUserFile_ShouldBeTrackedByGit</c>'s "not a work tree" early return.
/// DRK-1371 — the three template-repository probes that used to run here were retired: their subjects
/// (<see cref="TemplateRepositoryAssertions" />, <see cref="PackageArchitectureTests" />) moved into this
/// same assembly and now run directly as part of the solution's own test run.
/// </summary>
public sealed class ScaffoldingTests(TemplateScaffoldFixture fixture) : IClassFixture<TemplateScaffoldFixture>
{
    /// <summary>
    /// DRK-1271 §3 row 7: the scaffolded suite's only remaining filter clause. The two DRK-1263
    /// exclusions this constant used to carry are gone — that scaffold-time DEBUG-conditional
    /// stripping defect is fixed and merged, so those two scenarios run in the scaffolded suite
    /// like any other.
    /// </summary>
    private const string ExcludingIntegration = "FullyQualifiedName!~Integration";

    #region @existing / @new — scaffold, build, run the filtered suite

    [Fact]
    public void DefaultScaffoldName_StillProducesTodaysOutput()
    {
        if (!TemplateScaffoldFixture.Available) return;

        var build = fixture.BuildFor(null);
        build.ExitCode.ShouldBe(0, build.Output);

        var test = fixture.TestFor(null, ExcludingIntegration);
        test.ExitCode.ShouldBe(0, test.Output);
    }

    [Fact]
    public void DottedServiceName_ScaffoldsASolutionThatBuildsAndPassesItsTests()
    {
        if (!TemplateScaffoldFixture.Available) return;

        const string name = "DKNet.Accounts";

        var manifestAfterScaffold = fixture.ManifestAfterScaffold(name);
        var dir = fixture.ScaffoldDirFor(name);
        var manifestBeforeBuild = TemplateScaffoldFixture.Manifest(dir);
        manifestBeforeBuild.Count.ShouldBe(manifestAfterScaffold.Count);
        manifestBeforeBuild.ShouldBe(manifestAfterScaffold,
            "no file should have been hand-edited between scaffolding and building");

        var build = fixture.BuildFor(name);
        build.ExitCode.ShouldBe(0, build.Output);

        var test = fixture.TestFor(name, ExcludingIntegration);
        test.ExitCode.ShouldBe(0, test.Output);
    }

    [Theory]
    [InlineData("DKNet.Accounts")]
    [InlineData("DKNet.Accounts.Ledger")]
    [InlineData("Acme_Billing")]
    public void EveryAcceptedName_ProducesLegalIdentifiers(string name)
    {
        if (!TemplateScaffoldFixture.Available) return;

        var build = fixture.BuildFor(name);
        build.ExitCode.ShouldBe(0, build.Output);
    }

    #endregion

    #region @new — generated output carries only what a service owns

    [Fact]
    public void ScaffoldedArchitectureSuite_AssertsOnlyWhatAServiceOwns()
    {
        if (!TemplateScaffoldFixture.Available) return;

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
        if (!TemplateScaffoldFixture.Available) return;

        var dir = fixture.ScaffoldDirFor(null);
        var offenders = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => f.Replace(Path.DirectorySeparatorChar, '/').Contains("/Architecture/TemplateRepo/"))
            .ToArray();

        offenders.ShouldBeEmpty(
            "no file under Architecture/TemplateRepo/ may reach scaffolded output (§5 contract, R4): " +
            string.Join(", ", offenders));
    }

    #endregion
}
