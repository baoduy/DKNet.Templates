namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1263 acceptance tests — one test per §7 scenario, driving the scaffold engine through
/// <see cref="ScaffoldFixture"/> (the package's only inbound port: the `dotnet new` CLI).
/// Expected values below are literals copied from the brief's §7 Gherkin, never computed by
/// reading `template.json` or any production source.
/// </summary>
/// <remarks>
/// DRK-1271 §3 row 10: <c>GeneratedMigrationConfig_KeepsBothOfItsConditionals</c> and
/// <c>GeneratedLogConfig_KeepsAllThreeDebugBlocks</c> were retired here (dev-leader authorization,
/// DRK-1283 blocker thread) — DRK-1260 deliberately removed the conditionals they asserted on
/// (<c>DbMigration.cs</c> now has none at all; <c>R8</c> moved <c>LogConfigs.cs</c>'s console
/// exporters from <c>#if DEBUG</c> to <c>builder.Environment.IsDevelopment()</c>, leaving one block,
/// not three). Not re-pinned to the new shape: the invariant they protected — the scaffold engine
/// must not itself strip preprocessor directives — is already covered, more strongly, by the
/// byte-equality check in <see cref="GeneratedFile_IsRepositoryFileWithOnlySourceNameSubstituted"/>.
/// </remarks>
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
