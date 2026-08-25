using Minimal.AppServices;

namespace Minimal.App.Tests.Architecture;

/// <summary>
/// Structural invariants unique to the DRK-711 two-sample cycle (see docs/samples/manual-vs-automated.md):
/// the manual sample (<c>ManualSample</c>/<c>PurchaseOrder</c>) must stay 100% hand-written — no declarative
/// event/CRUD/DTO-generation attribute anywhere under it — and the automated sample
/// (<c>AutomatedSample</c>/<c>Product</c>) must stay 100% declarative — no hand-written <c>AddEvent</c> call
/// anywhere under it. Also covers the cycle's other named structural checks: no local LazyMapper copy,
/// the generator package reference, generated-code coverage exclusion, and the single migration baseline.
/// </summary>
public class SampleInvariantTests
{
    #region Methods

    [Fact]
    public void ManualSample_ShouldNotUseAnyDeclarativeGenerationAttribute()
    {
        var offenders = SourceFilesUnder("ManualSample")
            .Where(f => ContainsAny(File.ReadAllText(f), "[RaisesEvent", "[CrudCreate]", "[CrudUpdate]", "[GenerateDto"))
            .ToArray();

        offenders.ShouldBeEmpty(
            $"ManualSample must stay 100% hand-written — found a declarative generation attribute in: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void AutomatedSample_ShouldNotRaiseEventsByHand()
    {
        var offenders = SourceFilesUnder("AutomatedSample")
            .Where(f => File.ReadAllText(f).Contains("AddEvent(", StringComparison.Ordinal))
            .ToArray();

        offenders.ShouldBeEmpty(
            $"AutomatedSample must declare events only via [RaisesEvent] — found a hand-written AddEvent(...) call in: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void SampleAreas_ShouldNotCrossReferenceEachOther()
    {
        var manualOffenders = SourceFilesUnder("ManualSample")
            .Where(f => File.ReadAllText(f).Contains("AutomatedSample", StringComparison.Ordinal))
            .ToArray();
        var automatedOffenders = SourceFilesUnder("AutomatedSample")
            .Where(f => File.ReadAllText(f).Contains("ManualSample", StringComparison.Ordinal))
            .ToArray();

        manualOffenders.ShouldBeEmpty(
            $"ManualSample must not reference AutomatedSample: {string.Join(", ", manualOffenders)}");
        automatedOffenders.ShouldBeEmpty(
            $"AutomatedSample must not reference ManualSample: {string.Join(", ", automatedOffenders)}");
    }

    [Fact]
    public void NoLocalLazyMapperCopy_ShouldExist()
    {
        var offenders = new[] { "LazyMap.cs", "LazyResult.cs", "LazyMapExtensions.cs" }
            .SelectMany(f => Directory.GetFiles(SrcDir, f, SearchOption.AllDirectories))
            .ToArray();

        offenders.ShouldBeEmpty(
            $"The template must depend on DKNet.SlimBus.Extensions.LazyMapper, not a local copy: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void DKNetSlimBusGeneratorsPackage_ShouldBeReferenced()
    {
        var directoryPackagesPath = Path.Combine(SrcDir, "Directory.Packages.props");
        File.Exists(directoryPackagesPath).ShouldBeTrue();

        var content = File.ReadAllText(directoryPackagesPath);
        content.ShouldContain("DKNet.SlimBus.Generators");
    }

    [Fact]
    public void GeneratedCode_ShouldBeExcludedFromCoverageDenominator()
    {
        var runsettingsPath = Path.Combine(SrcDir, "coverage.runsettings");
        File.Exists(runsettingsPath).ShouldBeTrue();

        var content = File.ReadAllText(runsettingsPath);
        content.ShouldContain("*.g.cs");
    }

    [Fact]
    public void ExactlyOneMigration_ShouldExist_WithNoRemovedDemoStorage()
    {
        var migrationsDir = Path.Combine(SrcDir, "ApiEndpoints/Minimal.Infra/Migrations");
        Directory.Exists(migrationsDir).ShouldBeTrue();

        var migrationFiles = Directory.GetFiles(migrationsDir, "*.cs")
            .Where(f => !Path.GetFileName(f).EndsWith("ModelSnapshot.cs", StringComparison.Ordinal))
            .ToArray();

        // Exactly one migration = one <Timestamp>_<Name>.cs + its .Designer.cs.
        migrationFiles.Length.ShouldBe(2,
            $"Expected exactly one migration (2 files: migration + designer). Found: {string.Join(", ", migrationFiles.Select(Path.GetFileName))}");

        foreach (var file in migrationFiles)
        {
            var content = File.ReadAllText(file);
            content.Contains("CustomerProfile", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                $"{Path.GetFileName(file)} still references the removed CustomerProfile demo feature.");
            content.Contains("LoyaltyMembership", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                $"{Path.GetFileName(file)} still references the removed LoyaltyMembership demo feature.");
        }
    }

    [Fact]
    public void NoRemovedDemoEntityNames_ShouldAppearAnywhereUnderSrc()
    {
        var selfPath = Path.Combine(SrcDir, "ApiEndpoints/Minimal.App.Tests/Architecture/SampleInvariantTests.cs");

        var offenders = Directory.GetFiles(SrcDir, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !string.Equals(f, selfPath, StringComparison.Ordinal))
            .Where(f => ContainsAny(SafeReadAllText(f), "CustomerProfile", "LoyaltyMembership"))
            .ToArray();

        offenders.ShouldBeEmpty(
            $"Removed demo entity name found outside SampleInvariantTests: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void GeneratedCreateProductRequest_ShouldCarryNoActingUserProperty()
    {
        // Structural half of the security acceptance criterion (DRK-715 R1): the generated create request
        // has no property a caller could set to claim a different acting user — CreatedBy is stamped only
        // by DataOwnerHook, from the authenticated principal, at save time.
        var requestType = typeof(AppSetup).Assembly.GetTypes().SingleOrDefault(t => t.Name == "CreateProductRequest");

        requestType.ShouldNotBeNull(
            "CreateProductRequest is generated at build time (Minimal.AppServices.Crud, DKNet.SlimBus.Generators) — build the solution first.");

        requestType!.GetProperties()
            .Select(p => p.Name)
            .ShouldNotContain(n =>
                n.Contains("User", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("CreatedBy", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("UpdatedBy", StringComparison.OrdinalIgnoreCase));
    }

    private static string SrcDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    private static string SafeReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));

    private static IEnumerable<string> SourceFilesUnder(string sampleFolderName) =>
        Directory.GetFiles(Path.Combine(SrcDir, "ApiEndpoints"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => f.Contains(sampleFolderName, StringComparison.Ordinal));

    #endregion
}
