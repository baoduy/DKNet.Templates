using Microsoft.EntityFrameworkCore;
using Minimal.Infra.Contexts;

namespace Minimal.App.Tests.Architecture;

public class MigrationSchemaTests
{
    [Fact]
    public void Migration_ShouldCreate_ProSchema()
    {
        using var dbContext = new DbContextFactory().CreateDbContext([]);
        var schemas = dbContext.Model.GetEntityTypes()
            .Select(e => e.GetSchema())
            .Distinct()
            .ToHashSet();
        schemas.ShouldContain("pro");
    }

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
    public void Migration_ShouldCreate_CustomerProfilesTable()
    {
        using var dbContext = new DbContextFactory().CreateDbContext([]);
        var customerProfileType = dbContext.Model.GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName() == "CustomerProfiles");

        customerProfileType.ShouldNotBeNull();
        customerProfileType!.FindProperty("Id").ShouldNotBeNull();
        customerProfileType.FindProperty("Email").ShouldNotBeNull();
        customerProfileType.FindProperty("MembershipNo").ShouldNotBeNull();
        customerProfileType.FindProperty("Name").ShouldNotBeNull();
        customerProfileType.FindProperty("Phone").ShouldNotBeNull();
        customerProfileType.FindProperty("Avatar").ShouldNotBeNull();
        customerProfileType.FindProperty("BirthDay").ShouldNotBeNull();
    }

    [Fact]
    public void Migration_ShouldCreate_LoyaltyMembershipsTable()
    {
        using var dbContext = new DbContextFactory().CreateDbContext([]);
        var loyaltyMembershipType = dbContext.Model.GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName() == "LoyaltyMemberships");

        loyaltyMembershipType.ShouldNotBeNull();
        loyaltyMembershipType!.FindProperty("Id").ShouldNotBeNull();
        loyaltyMembershipType.FindProperty("MemberName").ShouldNotBeNull();
        loyaltyMembershipType.FindProperty("Tier").ShouldNotBeNull();
        loyaltyMembershipType.FindProperty("Points").ShouldNotBeNull();
    }

    [Fact]
    public void MemberName_ShouldHave_UniqueIndex()
    {
        using var dbContext = new DbContextFactory().CreateDbContext([]);
        var loyaltyMembershipType = dbContext.Model.GetEntityTypes()
            .First(e => e.GetTableName() == "LoyaltyMemberships");
        var memberNameProp = loyaltyMembershipType.FindProperty("MemberName")!;
        var isUnique = loyaltyMembershipType.GetIndexes()
            .Any(i => i.Properties.Count == 1
                      && i.Properties[0] == memberNameProp
                      && i.IsUnique);
        isUnique.ShouldBeTrue();
    }

    [Fact]
    public void Migration_ShouldHaveNoPendingModelChanges()
    {
        // Mirrors what `dotnet ef migrations has-pending-model-changes` checks at the CLI —
        // covers the acceptance criterion "no pending model changes remain for the loyalty membership".
        using var dbContext = new DbContextFactory().CreateDbContext([]);
        dbContext.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    [Fact]
    public void EfCoreModelSnapshot_ShouldReferenceNpgsql()
    {
        var srcDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "../../../../../ApiEndpoints/Minimal.Infra/Migrations"));

        var snapshotPath = Path.Combine(srcDir, "CoreDbContextModelSnapshot.cs");
        File.Exists(snapshotPath).ShouldBeTrue();

        var content = File.ReadAllText(snapshotPath);

        content.Contains("Npgsql.EntityFrameworkCore.PostgreSQL.Metadata", StringComparison.Ordinal)
            .ShouldBeTrue();
        content.Contains("SqlServer", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
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
    public void MembershipNo_ShouldHave_UniqueIndex()
    {
        using var dbContext = new DbContextFactory().CreateDbContext([]);
        var customerProfileType = dbContext.Model.GetEntityTypes()
            .First(e => e.GetTableName() == "CustomerProfiles");
        var membershipNoProp = customerProfileType.FindProperty("MembershipNo")!;
        var isUnique = customerProfileType.GetIndexes()
            .Any(i => i.Properties.Count == 1
                      && i.Properties[0] == membershipNoProp
                      && i.IsUnique);
        isUnique.ShouldBeTrue();
    }

    [Fact]
    public void Email_ShouldHave_UniqueIndex()
    {
        using var dbContext = new DbContextFactory().CreateDbContext([]);
        var customerProfileType = dbContext.Model.GetEntityTypes()
            .First(e => e.GetTableName() == "CustomerProfiles");
        var emailProp = customerProfileType.FindProperty("Email")!;
        var isUnique = customerProfileType.GetIndexes()
            .Any(i => i.Properties.Count == 1
                      && i.Properties[0] == emailProp
                      && i.IsUnique);
        isUnique.ShouldBeTrue();
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