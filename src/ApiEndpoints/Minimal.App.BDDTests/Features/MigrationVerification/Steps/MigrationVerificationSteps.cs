using Microsoft.Extensions.DependencyInjection;
using Minimal.Infra.Contexts;
using Minimal.Infra.Services;

namespace Minimal.App.BDDTests.Features.MigrationVerification.Steps;

[Binding]
public sealed class MigrationVerificationSteps(BddApiFactory factory)
{
    private IServiceScope _scope = null!;
    private CoreDbContext GetDbContext()
    {
        _scope ??= factory.Services.CreateScope();
        return _scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    }

    private string[] _values = null!;

    [Given("the API is running with PostgreSQL configuration")]
    public void GivenTheApiIsRunningWithPostgreSqlConfiguration()
    {
    }

    [When("the SequenceService generates the next MembershipNo")]
    public async Task WhenTheSequenceServiceGeneratesTheNextMembershipNo()
    {
        var dbContext = GetDbContext();
        var service = new MembershipService(dbContext);
        _values = [await service.NextValueAsync()];
    }

    [Then("a non-empty value should be returned")]
    public void ThenANonEmptyValueShouldBeReturned()
    {
        Assert.That(_values[0], Is.Not.Null.And.Not.Empty);
    }

    [When("the SequenceService generates {int} consecutive membership numbers")]
    public async Task WhenTheSequenceServiceGeneratesConsecutiveMembershipNumbers(int count)
    {
        var dbContext = GetDbContext();
        var service = new MembershipService(dbContext);

        var values = new List<string>();
        for (var i = 0; i < count; i++)
            values.Add(await service.NextValueAsync());

        _values = values.ToArray();
    }

    [Then("all {int} values should be unique")]
    public void ThenAllValuesShouldBeUnique(int count)
    {
        var unique = new HashSet<string>(_values);
        Assert.That(unique.Count, Is.EqualTo(count));
    }

    [Then("each value should be a valid GUID")]
    public void ThenEachValueShouldBeAValidGuid()
    {
        foreach (var value in _values)
            Assert.That(Guid.TryParse(value, out _), Is.True);
    }

    [When("I inspect the CustomerProfiles table model")]
    public void WhenIInspectTheCustomerProfilesTableModel()
    {
        _ = GetDbContext();
    }

    [Then("the table should be in schema {string}")]
    public void ThenTheTableShouldBeInSchema(string schema)
    {
        var entityType = GetDbContext().Model.GetEntityTypes()
            .First(e => e.GetTableName() == "CustomerProfiles");

        Assert.That(entityType.GetSchema(), Is.EqualTo(schema));
    }

    [Then("the table should have columns {string}")]
    public void ThenTheTableShouldHaveColumns(string columns)
    {
        var entityType = GetDbContext().Model.GetEntityTypes()
            .First(e => e.GetTableName() == "CustomerProfiles");

        var expectedColumns = columns.Split(",", StringSplitOptions.TrimEntries);
        var actualColumns = entityType.GetProperties()
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var col in expectedColumns)
            Assert.That(actualColumns, Does.Contain(col));
    }

    [Then("the Email column should have a unique index")]
    public void ThenTheEmailColumnShouldHaveAUniqueIndex()
    {
        var entityType = GetDbContext().Model.GetEntityTypes()
            .First(e => e.GetTableName() == "CustomerProfiles");

        var emailProp = entityType.FindProperty("Email")!;
        var hasIndex = entityType.GetIndexes()
            .Any(i => i.Properties.Count == 1
                      && i.Properties[0] == emailProp
                      && i.IsUnique);

        Assert.That(hasIndex, Is.True);
    }

    [Then("the MembershipNo column should have a unique index")]
    public void ThenTheMembershipNoColumnShouldHaveAUniqueIndex()
    {
        var entityType = GetDbContext().Model.GetEntityTypes()
            .First(e => e.GetTableName() == "CustomerProfiles");

        var membershipProp = entityType.FindProperty("MembershipNo")!;
        var hasIndex = entityType.GetIndexes()
            .Any(i => i.Properties.Count == 1
                      && i.Properties[0] == membershipProp
                      && i.IsUnique);

        Assert.That(hasIndex, Is.True);
    }
}