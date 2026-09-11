using Minimal.App.Tests.Integration.Support;
using Minimal.Domains.Features.AutomatedSample.Entities;
using Minimal.Infra.Contexts;
using Minimal.Share;

namespace Minimal.App.Tests.Integration.AutomatedSample.V1;

/// <summary>
/// The anonymous half of <see cref="ProductSensitiveDataTests"/>'s scenarios — kept in its own test class on
/// a bare <see cref="ApiFixture"/> (never combined with <see cref="AuthOnMultiSubjectApiFixture"/> in the same
/// class): that fixture's constructor flips <c>FeatureManagement:RequireAuthorization</c> on via a process-wide
/// environment variable, cleared only at class teardown (see its own remarks) — sharing a class with it would
/// leak "requires auth" into this fixture's host before it boots.
/// </summary>
public sealed class ProductSensitiveDataAnonymousTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string ProductName = "Espresso Machine";
    private const decimal ProductPrice = 899.00m;
    private const decimal SupplierCostPrice = 412.50m;
    private const string SupplierReferenceCode = "SUP-88421";

    [Fact]
    public async Task AnonymousCaller_ReceivesNeitherSensitiveProperty_ButStillReceivesNameAndPrice()
    {
        await fixture.ResetDatabaseAsync();
        var client = fixture.CreateClient();
        // The generated Product create route requires authentication unconditionally (pre-existing, unrelated
        // to this feature — every other Product test creates through an authenticated fixture). Seed directly,
        // mirroring SampleDataGenerator's own pattern for system-owned rows, so this scenario tests the read
        // path — the one this cycle changes.
        var productId = await SeedProductDirectlyAsync(fixture, ProductName);

        var response = await client.GetAsync($"/v1/products/{productId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        json.RootElement.TryGetProperty("supplierCostPrice", out _).ShouldBeFalse();
        json.RootElement.TryGetProperty("supplierReferenceCode", out _).ShouldBeFalse();
        json.RootElement.GetProperty("name").GetString().ShouldBe(ProductName);
        json.RootElement.GetProperty("price").GetDecimal().ShouldBe(ProductPrice);
    }

    /// <summary>
    /// Writes a system-owned <see cref="Product"/> straight through <see cref="CoreDbContext"/>, same pattern
    /// <c>SampleDataGenerator</c> uses for rows with no authenticated caller behind them — stamping the audit
    /// columns explicitly sidesteps <c>DataOwnerHook</c>'s need for an <c>HttpContext</c>, which a bare
    /// <see cref="IServiceScope"/> never has.
    /// </summary>
    private static async Task<Guid> SeedProductDirectlyAsync(ApiFixture apiFixture, string name)
    {
        using var scope = apiFixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var product = new Product(name, ProductPrice, SupplierCostPrice, SupplierReferenceCode);
        dbContext.Add(product);
        var entry = dbContext.Entry(product);
        entry.Property(nameof(Product.CreatedBy)).CurrentValue = SharedConsts.SystemAccount;
        entry.Property(nameof(Product.CreatedOn)).CurrentValue = DateTimeOffset.UtcNow;
        entry.Property(nameof(Product.OwnedBy)).CurrentValue = SharedConsts.SystemAccount;

        await dbContext.SaveChangesAsync();
        return product.Id;
    }
}
