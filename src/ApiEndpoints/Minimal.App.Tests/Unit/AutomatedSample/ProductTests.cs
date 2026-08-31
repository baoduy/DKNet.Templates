using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Minimal.Domains.Features.AutomatedSample.Entities;

namespace Minimal.App.Tests.Unit.AutomatedSample;

public class ProductTests
{
    #region Methods

    [Fact]
    public void Ctor_ShouldSetNameAndPrice()
    {
        var product = new Product("Widget", 9.99m);

        product.Name.ShouldBe("Widget");
        product.Price.ShouldBe(9.99m);
        product.IsDiscontinued.ShouldBeFalse();
        // Unlike PurchaseOrder (which threads a createdBy through AggregateRoot(string, ...) and gets its Id
        // eagerly), Product's [CrudCreate] constructor never calls that overload — its Id stays Guid.Empty
        // until EF Core's key value generator assigns one at SaveChanges time.
    }

    [Fact]
    public void ChangePrice_ShouldUpdatePrice()
    {
        var product = new Product("Widget", 9.99m);

        product.ChangePrice(12.50m);

        product.Price.ShouldBe(12.50m);
    }

    [Fact]
    public void CrudCreateConstructor_ShouldCarryDataAnnotations_ForTheGeneratorToForward()
    {
        // Grounds docs/samples/manual-vs-automated.md's claim that DataAnnotations forward 1:1 from the
        // [CrudCreate] constructor's parameters onto the generated request — this is the source the
        // generator reads, not the generated output itself.
        var ctor = typeof(Product)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single(c => c.GetParameters().Length == 2);

        var nameParam = ctor.GetParameters().Single(p => p.Name == "name");
        nameParam.GetCustomAttribute<RequiredAttribute>().ShouldNotBeNull();
        nameParam.GetCustomAttribute<StringLengthAttribute>()!.MaximumLength.ShouldBe(150);

        var priceParam = ctor.GetParameters().Single(p => p.Name == "price");
        var range = priceParam.GetCustomAttribute<RangeAttribute>();
        range.ShouldNotBeNull();
        range!.Minimum.ShouldBe(0.01);
    }

    [Fact]
    public void ChangePriceMethod_ShouldCarryTheSameRangeAttribute_AsTheConstructor()
    {
        var method = typeof(Product).GetMethod(nameof(Product.ChangePrice))!;
        var priceParam = method.GetParameters().Single(p => p.Name == "price");

        var range = priceParam.GetCustomAttribute<RangeAttribute>();
        range.ShouldNotBeNull();
        range!.Minimum.ShouldBe(0.01);
    }

    [Fact]
    public void Approve_ShouldStampTheActingUser()
    {
        var product = new Product("Widget", 9.99m);

        product.Approve("alice");

        product.UpdatedBy.ShouldBe("alice");
    }

    [Fact]
    public void Discontinue_ShouldMarkTheProductDiscontinued()
    {
        var product = new Product("Widget", 9.99m);

        product.Discontinue();

        product.IsDiscontinued.ShouldBeTrue();
    }

    [Fact]
    public void Discontinue_CalledTwice_ShouldStayDiscontinued_NotThrow()
    {
        // docs/samples/manual-vs-automated.md #4: a generated action has nowhere to hang a pre-condition,
        // so repeating Discontinue is a no-op, never a rejection.
        var product = new Product("Widget", 9.99m);

        product.Discontinue();
        product.Discontinue();

        product.IsDiscontinued.ShouldBeTrue();
    }

    #endregion
}
