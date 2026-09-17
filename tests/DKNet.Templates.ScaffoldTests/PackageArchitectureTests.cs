using System.Xml.Linq;

namespace DKNet.Templates.ScaffoldTests;

public class PackageArchitectureTests
{
    [Fact]
    public void NoSqlServerEfCorePackage_ShouldExist()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

        var csprojFiles = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories);

        var sqlServerRefs = csprojFiles
            .SelectMany(file =>
            {
                var doc = XDocument.Load(file);
                return doc.Descendants("PackageReference")
                    .Select(e => e.Attribute("Include")?.Value ?? "")
                    .Where(v => v.Contains("Microsoft.EntityFrameworkCore.SqlServer",
                        StringComparison.OrdinalIgnoreCase));
            })
            .Distinct()
            .ToArray();

        sqlServerRefs.ShouldBeEmpty();
    }

    [Fact]
    public void NoSqlServerTestcontainers_ShouldExist()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

        var csprojFiles = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories);

        var msSqlRefs = csprojFiles
            .SelectMany(file =>
            {
                var doc = XDocument.Load(file);
                return doc.Descendants("PackageReference")
                    .Select(e => e.Attribute("Include")?.Value ?? "")
                    .Where(v => v.Contains("MsSql", StringComparison.OrdinalIgnoreCase)
                                || v.Contains("SqlServer", StringComparison.OrdinalIgnoreCase));
            })
            .Distinct()
            .ToArray();

        msSqlRefs.ShouldBeEmpty();
    }

    [Fact]
    public void NpgsqlEfCorePackage_ShouldBeReferenced()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

        var directoryPackagesPath = Path.Combine(srcDir, "Directory.Packages.props");
        File.Exists(directoryPackagesPath).ShouldBeTrue();

        var content = File.ReadAllText(directoryPackagesPath);
        content.Contains("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void NoSqlServerAspireHostingPackage_ShouldExist()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

        var csprojFiles = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories);

        var sqlServerAspireRefs = csprojFiles
            .SelectMany(file =>
            {
                var doc = XDocument.Load(file);
                return doc.Descendants("PackageReference")
                    .Select(e => e.Attribute("Include")?.Value ?? "")
                    .Where(v => v.Contains("Aspire.Hosting.SqlServer",
                        StringComparison.OrdinalIgnoreCase));
            })
            .Distinct()
            .ToArray();

        sqlServerAspireRefs.ShouldBeEmpty();
    }

    [Fact]
    public void DebugGatedConfiguration_ShouldHaveDebugConditional()
    {
        var sourcePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "../../../../../src/ApiEndpoints/Minimal.Infra/Extensions/InfraSetup.cs"));

        File.Exists(sourcePath).ShouldBeTrue();
        var source = File.ReadAllText(sourcePath);

        source.ShouldContain("#if DEBUG");
        source.ShouldContain("EnableDetailedErrors()");
        source.ShouldContain("EnableSensitiveDataLogging()");
        source.ShouldContain("#endif");
    }

    [Fact]
    public void JobDispatch_ShouldRunAfterTheConfigurationSourcesAreAdded()
    {
        // A launched job resolves its settings from the same configuration sources the serving path does —
        // Azure App Configuration included — so dispatch has to sit after AddAzureAppConfig, and still ahead
        // of any service registration or host build (R4).
        var sourcePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "../../../../../src/ApiEndpoints/Minimal.Api/Program.cs"));

        File.Exists(sourcePath).ShouldBeTrue();
        var source = File.ReadAllText(sourcePath);

        var azureAppConfigAt = source.IndexOf("AddAzureAppConfig", StringComparison.Ordinal);
        var dispatchAt = source.IndexOf("JobSelector.Select", StringComparison.Ordinal);
        var servicesAt = source.IndexOf("builder.Services", StringComparison.Ordinal);

        azureAppConfigAt.ShouldBeGreaterThan(-1);
        dispatchAt.ShouldBeGreaterThan(azureAppConfigAt);
        servicesAt.ShouldBeGreaterThan(dispatchAt);
    }

    [Fact]
    public void AllDKNetPackageReferences_ShouldResolveToOneRelease()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

        var directoryPackagesPath = Path.Combine(srcDir, "Directory.Packages.props");
        File.Exists(directoryPackagesPath).ShouldBeTrue();

        var doc = XDocument.Load(directoryPackagesPath);
        var distinctVersions = PackagePinGuard.DistinctDkNetVersions(doc);

        distinctVersions.ShouldNotBeEmpty();
        distinctVersions.Count.ShouldBe(1,
            "DKNet packages must all resolve to the same release, found: " + string.Join(", ", distinctVersions));
    }

    [Fact]
    public void AllDKNetPackages_ShouldBePinnedTo_10_1_29()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

        var directoryPackagesPath = Path.Combine(srcDir, "Directory.Packages.props");
        File.Exists(directoryPackagesPath).ShouldBeTrue();

        var doc = XDocument.Load(directoryPackagesPath);
        var distinctVersions = PackagePinGuard.DistinctDkNetVersions(doc);

        distinctVersions.ShouldBe(["10.1.29"],
            "DKNet packages must all be pinned to 10.1.29, found: " + string.Join(", ", distinctVersions));
    }

    [Fact]
    public void AppConfig_ShouldWireIdempotencyToRedisOnlyWhenAConnectionStringIsConfigured()
    {
        // The @redis acceptance scenario ("with Redis configured, deduplication keys are held in Redis")
        // is a live-infrastructure behaviour this repo's own in-process harness cannot exercise — it is
        // out of the default suite by design (DRK-455 §5). This is the build-time stand-in: the branch
        // that switches to the Redis-backed store on a configured connection string still exists and the
        // Redis-free branch remains the default fallback.
        var sourcePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "../../../../../src/ApiEndpoints/Minimal.Api/Configs/AppConfig.cs"));

        File.Exists(sourcePath).ShouldBeTrue();
        var source = File.ReadAllText(sourcePath);

        source.ShouldContain("GetConnectionString(SharedConsts.RedisConnectionString)");
        source.ShouldContain("AddIdempotencyWithRedisStore(");
        source.ShouldContain("AddIdempotentKey(");
    }

    [Fact]
    public void EveryDKNetPackageVersion_ShouldBeReferencedByAtLeastOneProject()
    {
        // DRK-757: a PackageVersion pinned in Directory.Packages.props but never referenced is drift —
        // either a stale entry (this ticket's fix) or a project quietly relying on a transitive package.
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

        var directoryPackagesPath = Path.Combine(srcDir, "Directory.Packages.props");
        File.Exists(directoryPackagesPath).ShouldBeTrue();

        var dkNetPackageNames = XDocument.Load(directoryPackagesPath).Descendants("PackageVersion")
            .Select(e => e.Attribute("Include")?.Value ?? "")
            .Where(v => v.StartsWith("DKNet.", StringComparison.Ordinal))
            .ToArray();
        dkNetPackageNames.ShouldNotBeEmpty();

        var csprojFiles = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories);
        var referencedPackages = csprojFiles
            .SelectMany(file => XDocument.Load(file).Descendants("PackageReference")
                .Select(e => e.Attribute("Include")?.Value ?? ""))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unreferenced = dkNetPackageNames.Where(p => !referencedPackages.Contains(p)).ToArray();
        unreferenced.ShouldBeEmpty(
            "The following DKNet PackageVersion entries are pinned but never referenced by any project: " +
            string.Join(", ", unreferenced));
    }

    [Fact]
    public void NoPackageReference_ShouldCarryAVersionAttribute()
    {
        // DRK-757: versions are centrally managed in Directory.Packages.props (repo CLAUDE.md) — a
        // Version= on a PackageReference bypasses that and can silently drift from the pinned release.
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

        var csprojFiles = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories);

        var offenders = csprojFiles
            .SelectMany(file => XDocument.Load(file).Descendants("PackageReference")
                .Where(e => e.Attribute("Version") != null)
                .Select(e => $"{Path.GetFileName(file)}: {e.Attribute("Include")?.Value}"))
            .ToArray();

        offenders.ShouldBeEmpty(
            "PackageReference elements must not carry a Version attribute — versions are centrally " +
            "managed in Directory.Packages.props. Offenders: " + string.Join(", ", offenders));
    }
}