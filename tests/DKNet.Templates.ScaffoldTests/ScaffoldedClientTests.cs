using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1789 §5 — every solution scaffolded from dknet-minimal ships a typed Refit client for its own API,
/// the 4 endpoint skills keep it in step, and the DKNet.Templates release still ships only its 2 packages.
/// Scaffold scenarios shell out to the .NET SDK through <see cref="TemplateScaffoldFixture" /> and start with
/// the same <see cref="TemplateScaffoldFixture.Available" /> guard as <see cref="ScaffoldingTests" />.
/// Runs in its own non-parallel collection: its fixture installs the template with <c>dotnet new install</c>,
/// and doing that while another class's fixture installs or uninstalls the same template races on the
/// template engine's per-user settings.
/// </summary>
[Collection(nameof(ScaffoldedClientTests))]
public sealed class ScaffoldedClientTests(TemplateScaffoldFixture fixture) : IClassFixture<TemplateScaffoldFixture>
{
    private const string ExcludingIntegration = "FullyQualifiedName!~Integration";

    private static string SrcDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

    private static string RepoRoot => Path.GetFullPath(Path.Combine(SrcDir, ".."));

    private static XElement NuspecOf(ZipArchive package) =>
        XDocument.Load(package.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open())
            .Root!.Elements().Single(e => e.Name.LocalName == "metadata");

    private static IEnumerable<XElement> Named(XContainer container, string localName) =>
        container.Descendants().Where(e => e.Name.LocalName == localName);

    #region Scenario Outline: A scaffolded solution contains a client named after it

    [Theory]
    [InlineData("DKNet.Orders", "DKNet.Orders.Client")]
    [InlineData(null, "MyMinimalApp.Client")]
    public void ScaffoldedSolution_ContainsAClientProjectNamedAfterIt_AndBuildsAndPassesItsTests(
        string? name, string client)
    {
        if (!TemplateScaffoldFixture.Available) return;

        var dir = fixture.ScaffoldDirFor(name);
        File.Exists(Path.Combine(dir, "ApiEndpoints", client, $"{client}.csproj"))
            .ShouldBeTrue($"ApiEndpoints/{client}/{client}.csproj should be scaffolded");
        var sln = File.ReadAllText(Directory.GetFiles(dir, "*.sln", SearchOption.TopDirectoryOnly).Single());
        sln.ShouldContain($"= \"{client}\", \"ApiEndpoints\\{client}\\{client}.csproj\"");

        var build = fixture.BuildFor(name);
        build.ExitCode.ShouldBe(0, build.Output);

        var test = fixture.TestFor(name, ExcludingIntegration);
        test.ExitCode.ShouldBe(0, test.Output);
    }

    #endregion

    #region Scenario: The client packs into a NuGet package with its README

    [Fact]
    public void ScaffoldedClient_PacksIntoPackageDKNetOrdersClient_CarryingItsReadme_WithOnlyRefitAndMicrosoftDependencies()
    {
        if (!TemplateScaffoldFixture.Available) return;

        var clientDir = Path.Combine(fixture.ScaffoldDirFor("DKNet.Orders"), "ApiEndpoints", "DKNet.Orders.Client");
        var pack = fixture.PackFor(Path.Combine(clientDir, "DKNet.Orders.Client.csproj"));
        pack.ExitCode.ShouldBe(0, pack.Output);

        var packagePath = Directory.GetFiles(pack.OutputDir, "*.nupkg").ShouldHaveSingleItem();
        using var package = ZipFile.OpenRead(packagePath);
        var metadata = NuspecOf(package);
        Named(metadata, "id").Single().Value.ShouldBe("DKNet.Orders.Client");

        Named(metadata, "readme").Single().Value.ShouldBe("README.md");
        using var packedReadme = new MemoryStream();
        package.GetEntry("README.md").ShouldNotBeNull().Open().CopyTo(packedReadme);
        packedReadme.ToArray().ShouldBe(File.ReadAllBytes(Path.Combine(clientDir, "README.md")),
            "the package README should be the client project's own README.md");

        var dependencies = Named(metadata, "dependency").Select(d => d.Attribute("id")!.Value)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        dependencies.ShouldContain("Refit");
        dependencies.ShouldContain("Refit.HttpClientFactory");
        string[] allowed =
        [
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Http",
            "Refit",
            "Refit.HttpClientFactory"
        ];
        dependencies.Except(allowed, StringComparer.Ordinal).ShouldBeEmpty(
            "the client may depend only on Refit and the Microsoft HTTP and DI packages: " +
            string.Join(", ", dependencies));
    }

    [Fact]
    public void TemplatePackage_CarriesTheClientReadme_AndStillNoOtherProjectReadme()
    {
        var pack = fixture.PackFor(Path.Combine(SrcDir, "DKNet.Minimal.Template.csproj"));
        pack.ExitCode.ShouldBe(0, pack.Output);

        using var package = ZipFile.OpenRead(Directory.GetFiles(pack.OutputDir, "*.nupkg").ShouldHaveSingleItem());
        var projectReadmes = package.Entries.Select(e => e.FullName)
            .Where(n => n.StartsWith("content/ApiEndpoints/", StringComparison.Ordinal) &&
                        n.EndsWith("/README.md", StringComparison.Ordinal))
            .ToList();

        projectReadmes.ShouldBe(["content/ApiEndpoints/Minimal.Client/README.md"]);
    }

    #endregion

    #region Scenario Outline: The endpoint skills keep the client in step

    [Theory]
    [InlineData("dknet-endpoint", "adding the matching client method")]
    [InlineData("dknet-crud", "adding the matching client methods")]
    [InlineData("dknet-feature", "adding the matching client methods")]
    [InlineData("dknet-feature-remove", "removing the matching client methods")]
    public void EndpointSkill_IncludesTheClientStep_InTheSameChange(string skill, string clientStep)
    {
        var skillFile = Path.Combine(RepoRoot, "plugin", "skills", skill, "SKILL.md");
        var lines = File.ReadAllLines(skillFile);

        lines.Where(l =>
                l.Contains(clientStep, StringComparison.OrdinalIgnoreCase) &&
                l.Contains("in the same change", StringComparison.OrdinalIgnoreCase) &&
                l.Contains(".Client", StringComparison.Ordinal))
            .ShouldNotBeEmpty(
                $"{skill}/SKILL.md should carry a step line naming the .Client project, " +
                $"'{clientStep}' and 'in the same change'");

        // R5: the publish job rejects a SKILL.md over 500 lines.
        lines.Length.ShouldBeLessThanOrEqualTo(500);
    }

    #endregion

    #region Scenario: The DKNet.Templates release still ships only its 2 packages

    [Fact]
    public void RepoReleasePackaging_StillProducesExactlyItsTwoPackages()
    {
        // Given the DKNet.Templates repo with the client added to the scaffold.
        var templatePack = fixture.PackFor(Path.Combine(SrcDir, "DKNet.Minimal.Template.csproj"));
        templatePack.ExitCode.ShouldBe(0, templatePack.Output);
        var nugetIds = new List<string>();
        foreach (var nupkg in Directory.GetFiles(templatePack.OutputDir, "*.nupkg"))
        {
            using var package = ZipFile.OpenRead(nupkg);
            package.GetEntry("content/ApiEndpoints/Minimal.Client/Minimal.Client.csproj").ShouldNotBeNull();
            nugetIds.Add(Named(NuspecOf(package), "id").Single().Value);
        }

        // When the repo's release packaging runs: every `dotnet pack` and `npm publish` the workflows declare.
        var workflowLines = Directory.GetFiles(Path.Combine(RepoRoot, ".github", "workflows"), "*.yml")
            .SelectMany(File.ReadAllLines)
            .Where(l => !l.TrimStart().StartsWith('#'))
            .ToList();
        workflowLines
            .Select(l => Regex.Match(l, @"dotnet pack\s+(\S+)"))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ShouldBe(["src/DKNet.Minimal.Template.csproj"]);
        workflowLines.Count(l => l.Contains("npm publish", StringComparison.Ordinal)).ShouldBe(1);

        var npmPack = TemplateScaffoldFixture.Run("npm", "pack --dry-run --json --loglevel=silent", RepoRoot);
        npmPack.ExitCode.ShouldBe(0, npmPack.Output);
        using var npmPackages = JsonDocument.Parse(npmPack.Output);
        // npm <= 11 prints an array of packed packages; npm 12 an object keyed by package name.
        var npmNames = npmPackages.RootElement.ValueKind == JsonValueKind.Array
            ? npmPackages.RootElement.EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToList()
            : npmPackages.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        // Then it produces exactly 2 packages: the template and the skills package.
        nugetIds.Concat(npmNames).ShouldBe(["DKNet.Minimal.Template", "@drunkcoding/dknet-implementation-skills"]);
    }

    #endregion

    #region Scenario: The scaffolded solution ships no pipeline

    [Fact]
    public void ScaffoldedSolution_ContainsNoPipelineDefinition()
    {
        if (!TemplateScaffoldFixture.Available) return;

        var dir = fixture.ScaffoldDirFor("DKNet.Orders");
        File.Exists(Path.Combine(dir, "DKNet.Orders.sln")).ShouldBeTrue();

        string[] pipelineFileNames =
        [
            "azure-pipelines.yml", "azure-pipelines.yaml", ".gitlab-ci.yml", "Jenkinsfile",
            "bitbucket-pipelines.yml", "appveyor.yml", ".travis.yml"
        ];
        var pipelines = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(f => !f.Split('/').Any(s => s is "bin" or "obj"))
            .Where(f => f.StartsWith(".github/workflows/", StringComparison.Ordinal) ||
                        f.StartsWith(".circleci/", StringComparison.Ordinal) ||
                        f.StartsWith(".azure-pipelines/", StringComparison.Ordinal) ||
                        pipelineFileNames.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .ToList();

        pipelines.ShouldBeEmpty("the scaffolded solution must ship no pipeline: " + string.Join(", ", pipelines));
    }

    #endregion
}

[CollectionDefinition(nameof(ScaffoldedClientTests), DisableParallelization = true)]
public sealed class ScaffoldedClientTestsCollection;
