using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Events;
using Minimal.Domains.Share;

namespace Minimal.Domains.Features.AutomatedSample.Entities;

/// <summary>
/// Represents a sellable product aggregate root.
/// </summary>
/// <remarks>
/// Creation and price changes are declared via <see cref="RaisesEventAttribute"/> and raised automatically
/// by the DKNet events hook on save — nothing here raises an event by hand. The <c>[CrudCreate]</c>
/// constructor's parameter list becomes the generated create request's payload, so it deliberately carries
/// no acting-user parameter — that would make the acting user caller-settable.
/// </remarks>
[RaisesEvent(EventOperations.Created, Include = [nameof(Id), nameof(Name), nameof(Price)])]
[RaisesEvent(EventOperations.Updated, nameof(Price))]
public class Product : AggregateRoot
{
    #region Constructors

    /// <summary>
    /// Creates a new <see cref="Product"/>.
    /// </summary>
    /// <param name="name">The product name. Unique.</param>
    /// <param name="price">The unit price. Must be positive.</param>
    [CrudCreate]
    public Product([Required, StringLength(150)] string name, [Range(0.01, double.MaxValue)] decimal price)
    {
        Name = name;
        Price = price;
    }

    /// <inheritdoc />
    protected Product()
    {
    }

    #endregion

    #region Properties

    /// <summary>Gets the product name.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Gets the product's current unit price.</summary>
    public decimal Price { get; private set; }

    /// <summary>Gets whether the product has been discontinued.</summary>
    public bool IsDiscontinued { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Changes the product's price.
    /// </summary>
    /// <param name="price">The new unit price. Must be positive.</param>
    [CrudUpdate]
    public void ChangePrice([Range(0.01, double.MaxValue)] decimal price) => Price = price;

    #endregion
}
