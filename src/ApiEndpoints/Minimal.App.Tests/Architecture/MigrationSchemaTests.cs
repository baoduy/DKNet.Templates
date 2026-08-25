using Microsoft.EntityFrameworkCore;
using Minimal.Infra.Contexts;

namespace Minimal.App.Tests.Architecture;

public class MigrationSchemaTests
{
    [Fact]
    public void Migration_ShouldCreate_SeqSchema()
    {
        using var dbContext = new DbContextFactory().CreateDbContext([]);
        var sequences = dbContext.Model.GetSequences()
            .Select(s => s.Schema)
            .Distinct()
            .ToHashSet();
        sequences.ShouldContain("seq");
    }

    [Fact]
    public void Migration_ShouldHave_OnlySeqMembership_NotSeqNone()
    {
        using var dbContext = new DbContextFactory().CreateDbContext([]);
        var sequenceNames = dbContext.Model.GetSequences()
            .Select(s => s.Name)
            .ToArray();

        sequenceNames.ShouldContain("Seq_Membership");
        sequenceNames.Any(s => s.Contains("None", StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse();
    }

    [Fact]
    public void EfCoreModel_ShouldTarget_PostgreSqlProvider()
    {
        using var dbContext = new DbContextFactory().CreateDbContext([]);
        var providerName = dbContext.Database.ProviderName;
        providerName.ShouldNotBeNullOrWhiteSpace();
        providerName!.Contains("SqlServer", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    [Fact]
    public void AppHost_ShouldReference_PostgreSqlNotSqlServer()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "../../../../../ApiEndpoints/Minimal.AppHost"));

        var csprojPath = Path.Combine(srcDir, "Minimal.AppHost.csproj");
        File.Exists(csprojPath).ShouldBeTrue();

        var content = File.ReadAllText(csprojPath);

        content.Contains("Aspire.Hosting.PostgreSQL", StringComparison.Ordinal).ShouldBeTrue();
        content.Contains("Aspire.Hosting.SqlServer", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    [Fact]
    public void InfraCsproj_ShouldReference_NpgsqlNotSqlServer()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "../../../../../ApiEndpoints/Minimal.Infra"));

        var csprojPath = Path.Combine(srcDir, "Minimal.Infra.csproj");
        File.Exists(csprojPath).ShouldBeTrue();

        var content = File.ReadAllText(csprojPath);

        content.Contains("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal).ShouldBeTrue();
        content.Contains("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }
}