using System.Xml.Linq;

namespace Minimal.App.Tests.Architecture;

public class PackageArchitectureTests
{
    [Fact]
    public void NoSqlServerEfCorePackage_ShouldExist()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../.."));

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
            Path.Combine(AppContext.BaseDirectory, "../../../../.."));

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
            Path.Combine(AppContext.BaseDirectory, "../../../../.."));

        var directoryPackagesPath = Path.Combine(srcDir, "Directory.Packages.props");
        File.Exists(directoryPackagesPath).ShouldBeTrue();

        var content = File.ReadAllText(directoryPackagesPath);
        content.Contains("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void NoSqlServerAspireHostingPackage_ShouldExist()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../.."));

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
                "../../../../../ApiEndpoints/Minimal.Infra/Extensions/InfraSetup.cs"));

        File.Exists(sourcePath).ShouldBeTrue();
        var source = File.ReadAllText(sourcePath);

        source.ShouldContain("#if DEBUG");
        source.ShouldContain("EnableDetailedErrors()");
        source.ShouldContain("EnableSensitiveDataLogging()");
        source.ShouldContain("#endif");
    }

    [Fact]
    public void AllDKNetPackageReferences_ShouldResolveToOneRelease()
    {
        const string expectedVersion = "10.1.10";

        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../.."));

        var directoryPackagesPath = Path.Combine(srcDir, "Directory.Packages.props");
        File.Exists(directoryPackagesPath).ShouldBeTrue();

        var doc = XDocument.Load(directoryPackagesPath);
        var dkNetVersions = doc.Descendants("PackageVersion")
            .Where(e => (e.Attribute("Include")?.Value ?? "").StartsWith("DKNet.", StringComparison.Ordinal))
            .Select(e => new { Package = e.Attribute("Include")!.Value, Version = e.Attribute("Version")?.Value })
            .ToArray();

        dkNetVersions.ShouldNotBeEmpty();

        var offenders = dkNetVersions.Where(p => p.Version != expectedVersion).ToArray();
        offenders.ShouldBeEmpty(
            $"The following DKNet packages are not pinned to {expectedVersion}: " +
            string.Join(", ", offenders.Select(p => $"{p.Package}={p.Version}")));
    }

    [Theory]
    [InlineData("Enroll.cs")]
    [InlineData("Change.cs")]
    [InlineData("Withdraw.cs")]
    public void LoyaltyMembershipCommandHandlers_ShouldNotRaiseEventsByHand(string fileName)
    {
        // The spec's signal for "declared events, not hand-raised": no line in these command handlers
        // calls AddEvent — the three events are raised by the DKNet events hook via [RaisesEvent] on the
        // entity itself (see LoyaltyMembershipTests.LoyaltyMembership_ShouldDeclareItsThreeEventsViaAttribute_NotByHand).
        var sourcePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                $"../../../../../ApiEndpoints/Minimal.AppServices/LoyaltyMemberships/V1/Actions/{fileName}"));

        File.Exists(sourcePath).ShouldBeTrue();
        var source = File.ReadAllText(sourcePath);

        source.ShouldNotContain(".AddEvent(");
        source.ShouldNotContain(".AddEvent<");
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
                "../../../../../ApiEndpoints/Minimal.Api/Configs/AppConfig.cs"));

        File.Exists(sourcePath).ShouldBeTrue();
        var source = File.ReadAllText(sourcePath);

        source.ShouldContain("GetConnectionString(SharedConsts.RedisConnectionString)");
        source.ShouldContain("AddIdempotencyWithRedisStore(");
        source.ShouldContain("AddIdempotentKey(");
    }

    [Fact]
    public void ServiceBusSetup_ShouldStillProduceProfileCreatedEventToItsExternalTopic()
    {
        // Regression guard for the invariant "the ProfileCreated event still... produces to the
        // external topic for the broker" — CustomerProfileEventPublishingTests covers the in-process
        // subscriber side at runtime; reaching a real Azure Service Bus topic needs live infrastructure
        // this repo's own harness cannot provide, so the topic wiring itself is verified at the source level.
        var sourcePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "../../../../../ApiEndpoints/Minimal.Infra/Extensions/ServiceBusSetup.cs"));

        File.Exists(sourcePath).ShouldBeTrue();
        var source = File.ReadAllText(sourcePath);

        source.ShouldContain("Produce<CustomerProfileCreatedEvent>(o => o.DefaultTopic(\"profile-tp\"))");
        source.ShouldContain("Consume<CustomerProfileCreatedEvent>(");
        source.ShouldContain("WithConsumer<CustomerProfileCreatedEmailNotificationHandler>()");
    }
}