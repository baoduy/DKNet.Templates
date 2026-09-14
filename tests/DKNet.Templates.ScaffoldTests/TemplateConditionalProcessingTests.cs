using System.Text.RegularExpressions;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1263 acceptance tests — one test per §7 scenario, driving the scaffold engine through
/// <see cref="ScaffoldFixture"/> (the package's only inbound port: the `dotnet new` CLI).
/// Expected values below are literals copied from the brief's §7 Gherkin, never computed by
/// reading `template.json` or any production source.
/// </summary>
[Trait("Feature", "template-scaffold")]
public class TemplateConditionalProcessingTests(ScaffoldFixture fixture) : IClassFixture<ScaffoldFixture>
{
    private const string InfraSetupGenerated = "ApiEndpoints/Contoso.Infra/Extensions/InfraSetup.cs";
    private const string InfraSetupRepository = "src/ApiEndpoints/Minimal.Infra/Extensions/InfraSetup.cs";
    private const string DbMigrationGenerated = "ApiEndpoints/Contoso.Api/Configs/DbMigration.cs";
    private const string DbMigrationRepository = "src/ApiEndpoints/Minimal.Api/Configs/DbMigration.cs";
    private const string LogConfigsGenerated = "ApiEndpoints/Contoso.Api/Configs/LogConfigs.cs";
    private const string LogConfigsRepository = "src/ApiEndpoints/Minimal.Api/Configs/LogConfigs.cs";

    [Fact]
    [Trait("Category", "existing")]
    public void TemplateRepositoryOwnSuite_StillSeesItsDebugConditional()
    {
        // Mirrors PackageArchitectureTests.DebugGatedConfiguration_ShouldHaveDebugConditional —
        // this cycle must never touch the repository's own InfraSetup.cs, only how it scaffolds.
        var source = fixture.ReadRepository(InfraSetupRepository);

        source.ShouldContain("#if DEBUG");
        source.ShouldContain("EnableDetailedErrors()");
        source.ShouldContain("EnableSensitiveDataLogging()");
        source.ShouldContain("#endif");
    }

    [Fact]
    [Trait("Category", "existing")]
    public void Scaffolding_StillSubstitutesSourceName()
    {
        var generated = fixture.ReadGenerated(DbMigrationGenerated);

        generated.ShouldContain("namespace Contoso.Api.Configs;");
        generated.ShouldNotContain("namespace Minimal.Api.Configs;");
    }

    [Fact]
    [Trait("Category", "new")]
    public void GeneratedInfrastructureSetup_KeepsItsDebugBlock()
    {
        var generated = fixture.ReadGenerated(InfraSetupGenerated);

        generated.ShouldContain("#if DEBUG");
        generated.ShouldContain("builder.EnableDetailedErrors().EnableSensitiveDataLogging();");
        generated.ShouldContain("#endif");
    }

    [Fact]
    [Trait("Category", "new")]
    public void GeneratedMigrationConfig_KeepsBothOfItsConditionals()
    {
        var generated = fixture.ReadGenerated(DbMigrationGenerated);

        generated.ShouldContain("#if DEBUG");
        generated.ShouldContain("var isMigration = features.RunDbMigrationWhenAppStart;");
        generated.ShouldContain("#else");
        generated.ShouldContain("#if !DEBUG");

        var guardStart = generated.IndexOf("#if !DEBUG", StringComparison.Ordinal);
        var guardEnd = generated.IndexOf("#endif", guardStart, StringComparison.Ordinal);
        guardEnd.ShouldBeGreaterThan(guardStart);

        var exitIndex = generated.IndexOf("Environment.Exit(0);", StringComparison.Ordinal);
        exitIndex.ShouldBeInRange(guardStart, guardEnd);
    }

    [Fact]
    [Trait("Category", "new")]
    public void GeneratedLogConfig_KeepsAllThreeDebugBlocks()
    {
        var generated = fixture.ReadGenerated(LogConfigsGenerated);

        Regex.Matches(generated, Regex.Escape("#if DEBUG")).Count.ShouldBe(3);
        generated.ShouldContain("builder.Logging.AddConsole();");
        generated.ShouldContain("tracing.AddConsoleExporter();");
        generated.ShouldContain("metrics.AddConsoleExporter();");
    }

    [Theory]
    [Trait("Category", "new")]
    [InlineData(InfraSetupGenerated, InfraSetupRepository)]
    [InlineData(DbMigrationGenerated, DbMigrationRepository)]
    [InlineData(LogConfigsGenerated, LogConfigsRepository)]
    public void GeneratedFile_IsRepositoryFileWithOnlySourceNameSubstituted(string generatedPath, string repositoryPath)
    {
        var generated = fixture.ReadGenerated(generatedPath);
        var expected = fixture.ReadRepository(repositoryPath).Replace("Minimal", "Contoso");

        generated.ShouldBe(expected);
    }
}
