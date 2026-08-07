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
}