using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.DataAuthorization;
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
/// A <c>[CrudAction]</c> method publishes a POST at the by-id route plus a segment, returning 200 with the
/// entity DTO. <see cref="Approve"/> overrides the segment (<c>approval</c>) while keeping the default POST
/// verb; <see cref="Discontinue"/> keeps the default method-derived segment but overrides the verb to PUT.
/// Implements <see cref="IOwnedBy"/> so <c>DataOwnerAuthQuery</c>'s global read filter (row-level isolation)
/// applies to it; <c>DataOwnerHook</c> stamps <see cref="OwnedBy"/> from the same ownership key as
/// <c>CreatedBy</c> on insert.
/// </remarks>
[RaisesEvent(EventOperations.Created, Include = [nameof(Id), nameof(Name), nameof(Price)])]
[RaisesEvent(EventOperations.Updated, nameof(Price))]
[RaisesEvent(EventOperations.Updated, nameof(IsDiscontinued))]
public class Product : AggregateRoot, IOwnedBy
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

    /// <summary>Gets the ownership key of the caller who created this product — stamped by <c>DataOwnerHook</c>.</summary>
    public string OwnedBy { get; private set; } = string.Empty;

    #endregion

    #region Methods

    /// <summary>
    /// Changes the product's price.
    /// </summary>
    /// <param name="price">The new unit price. Must be positive.</param>
    [CrudUpdate]
    public void ChangePrice([Range(0.01, double.MaxValue)] decimal price) => Price = price;

    /// <summary>
    /// Approves the product, stamping the acting user.
    /// </summary>
    /// <param name="byUser">The approving user.</param>
    [CrudAction("approval")]
    public void Approve(string byUser) => SetUpdatedBy(byUser);

    /// <summary>
    /// Discontinues the product. Idempotent — calling it again is a no-op.
    /// </summary>
    [CrudAction(Verb = CrudActionVerb.Put)]
    public void Discontinue() => IsDiscontinued = true;

    #endregion
}
