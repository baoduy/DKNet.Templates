using DKNet.EfCore.DataAuthorization;
using Microsoft.EntityFrameworkCore;
using Minimal.Domains.Features.ManualSample.Entities;
using Minimal.Infra.Contexts;

namespace Minimal.App.Tests.Unit.ManualSample;

/// <summary>
/// DRK-1901: a purchase order is only saved when it has an owner. With a data-owner provider that resolves no
/// ownership key (an authenticated caller with no subject claim), a new order with no <c>OwnedBy</c> is refused
/// before EF Core saves it, while a seeded order that already carries its owner is saved unchanged.
/// </summary>
public sealed class PurchaseOrderOwnershipTests
{
    #region Methods

    [Fact]
    public async Task NewOrder_WithNoResolvableOwnershipKey_IsRefused_AndNothingIsSaved()
    {
        var options = NewOptions();
        await using (var db = new CoreDbContext(options, [new NoKeyDataOwnerProvider()]))
        {
            db.Add(new PurchaseOrder("Orphan Traders", 1.23m, "alice"));

            await Should.ThrowAsync<OwnershipRequiredException>(() => db.SaveChangesAsync());
        }

        await using var reader = new CoreDbContext(options);
        (await reader.Set<PurchaseOrder>().IgnoreQueryFilters().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public void NewOrder_WithNoResolvableOwnershipKey_IsRefused_OnSynchronousSave()
    {
        using var db = new CoreDbContext(NewOptions(), [new NoKeyDataOwnerProvider()]);
        db.Add(new PurchaseOrder("Orphan Traders", 1.23m, "alice"));

        Should.Throw<OwnershipRequiredException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task SeededOrder_CarryingItsOwnOwner_IsSaved_AndKeepsThatOwner_WhenNoKeyResolves()
    {
        var options = NewOptions();
        var id = new Guid("6E6F4D3C-1B7E-4C7A-9F1D-8A2B5C6D7E01");
        await using (var db = new CoreDbContext(options, [new NoKeyDataOwnerProvider()]))
        {
            db.Add(new PurchaseOrder(id, "Acme Pte Ltd", 1250.00m, "System"));
            await db.SaveChangesAsync();
        }

        await using var reader = new CoreDbContext(options);
        var saved = await reader.Set<PurchaseOrder>().IgnoreQueryFilters().SingleAsync(p => p.Id == id);
        saved.OwnedBy.ShouldBe("System");
    }

    private static DbContextOptions NewOptions() =>
        new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"po-ownership-{Guid.NewGuid():N}")
            .UseAutoConfigModel([typeof(CoreDbContext).Assembly])
            .Options;

    #endregion

    private sealed class NoKeyDataOwnerProvider : IDataOwnerProvider
    {
        public string? GetOwnershipKey() => null;
    }
}
