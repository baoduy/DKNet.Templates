using System.Xml.Linq;
using Minimal.Api.ApiEndpoints;

namespace Minimal.App.Tests.Architecture;

/// <summary>
/// Covers DRK-500's "the template depends on the published package rather than a copy" and "the two test
/// suites share one set of helpers" acceptance scenarios.
/// </summary>
public class PackageAdoptionTests
{
    #region Methods

    [Theory]
    [InlineData(typeof(CustomerProfileV1Endpoint))]
    [InlineData(typeof(LoyaltyMembershipV1Endpoint))]
    public void EndpointConfigs_ShouldImplementThePackagesInterface_NotATemplateCopy(Type endpointType)
    {
        var endpointConfigInterface = endpointType.GetInterface("IEndpointConfig");

        endpointConfigInterface.ShouldNotBeNull($"{endpointType.Name} should implement IEndpointConfig.");
        endpointConfigInterface.Assembly.GetName().Name.ShouldBe("DKNet.AspCore.Extensions",
            $"{endpointType.Name} should implement the package's IEndpointConfig, not a template-local one.");
    }

    [Theory]
    [InlineData("Minimal.Api/Configs/Endpoints/EndpointConfig.cs")]
    [InlineData("Minimal.Api/Configs/Endpoints/FluentEndpointMapperExtensions.cs")]
    [InlineData("Minimal.Api/Configs/Endpoints/IEndpointConfig.cs")]
    [InlineData("Minimal.Api/Configs/Endpoints/PagedResult.cs")]
    [InlineData("Minimal.Api/Extensions/ProblemDetailsExtensions.cs")]
    [InlineData("Minimal.Api/Extensions/ResultResponseExtensions.cs")]
    public void PackagedPlumbing_ShouldNotHaveATemplateCopy(string relativePath)
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

        File.Exists(Path.Combine(srcDir, "ApiEndpoints", relativePath)).ShouldBeFalse(
            $"{relativePath} duplicates plumbing the package now provides — it should have been removed.");
    }

    [Theory]
    [InlineData("Minimal.App.Tests/Minimal.App.Tests.csproj")]
    [InlineData("Minimal.App.BDDTests/Minimal.App.BDDTests.csproj")]
    public void BothTestSuites_ShouldReferenceTheSharedTestSupportProject(string relativeCsprojPath)
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var csprojPath = Path.Combine(srcDir, "ApiEndpoints", relativeCsprojPath);

        File.Exists(csprojPath).ShouldBeTrue();
        var doc = XDocument.Load(csprojPath);

        var referencesTestSupport = doc.Descendants("ProjectReference")
            .Any(e => (e.Attribute("Include")?.Value ?? "").Contains("Minimal.App.TestSupport",
                StringComparison.OrdinalIgnoreCase));

        referencesTestSupport.ShouldBeTrue($"{relativeCsprojPath} should reference Minimal.App.TestSupport.");
    }

    [Theory]
    [InlineData("Minimal.App.Tests/Integration/Support/Eventually.cs")]
    [InlineData("Minimal.App.Tests/Integration/Support/TestLogCapture.cs")]
    [InlineData("Minimal.App.Tests/Integration/Support/TestMembershipService.cs")]
    [InlineData("Minimal.App.BDDTests/Support/Eventually.cs")]
    [InlineData("Minimal.App.BDDTests/Support/TestLogCapture.cs")]
    [InlineData("Minimal.App.BDDTests/Support/TestMembershipService.cs")]
    public void SharedTestHelpers_ShouldNotHaveAPerSuiteCopy(string relativePath)
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

        File.Exists(Path.Combine(srcDir, "ApiEndpoints", relativePath)).ShouldBeFalse(
            $"{relativePath} duplicates a helper that now lives in Minimal.App.TestSupport.");
    }

    [Fact]
    public void StatusCountsEndpointMapper_ShouldStillWireGetStatusCountsAsATemplateLocalGetEndpoint()
    {
        // "Also verify" item on DRK-500: MapGetStatusCounts moved into its own template-local file while
        // everything else moved into the package (status-count endpoints stay template-local per §4).
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var sourcePath = Path.Combine(srcDir,
            "ApiEndpoints/Minimal.Api/Configs/Endpoints/StatusCountsEndpointMapperExtensions.cs");

        File.Exists(sourcePath).ShouldBeTrue();
        var source = File.ReadAllText(sourcePath);

        source.ShouldContain("public RouteHandlerBuilder MapGetStatusCounts<TEntity>(");
        source.ShouldContain("app.MapGet(");
        source.ShouldContain("repo.GetStatusCounts<TEntity>(");
    }

    #endregion
}
