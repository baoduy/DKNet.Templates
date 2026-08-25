---
name: dknet-domain-entity
description: Create DDD domain entities following this project's AggregateRoot/DomainEntity inheritance pattern. Use when adding a new domain entity or owned type to Minimal.Domains.
---

# Skill: Domain Entity Definition

Create domain entities that integrate with this project's DDD infrastructure — `AggregateRoot`, `DomainEntity`, and owned value objects.

If the aggregate boundary, entity-vs-value-object choice, or invariant placement isn't obvious, read **dknet-ddd-principles** first — this skill covers class mechanics, not those judgment calls.

---

## When to Use

- Adding a new aggregate root entity (e.g., `PurchaseOrder`, `Product`, Invoice)
- Adding a new owned value object (a plain nested type with no identity of its own, e.g. a `ShippingDetails` record embedded in an order)
- Adding domain service interfaces for the new feature

## Inputs Required

Before starting, gather:

1. **Entity name** (PascalCase, singular): e.g., `Order`, `Invoice`
2. **Feature name** (plural folder): e.g., `Orders`, `Invoices`
3. **Properties**: name, C# type, required/optional
4. **Mutation methods**: what fields change after creation?
5. **Schema prefix**: short string for `DomainSchemas` (e.g., `"ord"`)
6. **Domain services needed**: any external ID generators or lookups?

---

## Project Conventions (from actual codebase)

### Inheritance Hierarchy

```
AuditedEntity<Guid>        ← from DKNet.EfCore.Abstractions.Entities
  └── DomainEntity          ← Minimal.Domains.Share (abstract, Guid Id + audit)
       └── AggregateRoot    ← Minimal.Domains.Share (abstract, Guid auto-gen)
```

### Key Rules

- Entities inherit from `AggregateRoot` (or `DomainEntity` for non-root entities); mark the class `sealed` unless you have a concrete reason another type needs to derive from it (`PurchaseOrder` is `sealed`; `Product` isn't, only because its generator-driven constructor needed no further customization either way — sealing is the default, not the exception)
- Properties use `{ get; private set; }` — mutation happens ONLY through named methods
- Constructor sets immutable fields; a mutation method (e.g. `ChangeAmount`, `Cancel`) handles anything that changes later
- `SetCreatedBy(userId)` and `SetUpdatedBy(userId)` are inherited from `AuditedEntity`
- Entity uses `AddEvent(...)` to publish domain events by hand (inherited from base) — see the callout below for the declarative alternative
- The public constructor calls `base(byUser)` — `AggregateRoot`'s own constructor auto-generates the Id (`Guid.NewGuid()`), you never assign it yourself. The `internal Guid id` overload (`base(id, byUser)`) exists only for rehydrating a known identity, e.g. static seed data — see `PurchaseOrder`'s internal constructor.

> **Alternative: declare events/CRUD instead of hand-writing them.** Everything on this page describes writing the entity, its events, and its mutation methods by hand — the pattern used by `Minimal.Domains/Features/ManualSample/Entities/PurchaseOrder.cs`. An entity can instead declare `[RaisesEvent(EventOperations.Created, Include = [...])]` / `[RaisesEvent(EventOperations.Updated, nameof(Prop))]` at the class level, mark its constructor `[CrudCreate]`, and mark a mutation method `[CrudUpdate]` — the `DKNet.SlimBus.Generators` analyzer then generates the event record, the create/update request+handler, and the CRUD routes for you. See `Minimal.Domains/Features/AutomatedSample/Entities/Product.cs`. The trade-off: the generated create/update request's DataAnnotations (`[Required]`, `[Range]`, etc.) are **not enforced** under this template's own endpoint-registration convention — see `docs/samples/manual-vs-automated.md` for why. Pick the manual, hand-written shape whenever you need that validation to actually run, or any business rule beyond what a DataAnnotations attribute can express.

### File Location

```
src/ApiEndpoints/Minimal.Domains/
├── Features/
│   └── {Feature}/
│       └── Entities/
│           ├── {Entity}.cs              ← Aggregate root
│           ├── {OwnedType}.cs           ← Owned value objects (optional)
│           └── {ChildEntity}.cs         ← Non-root entities (optional)
├── Services/
│   ├── I{Service}.cs                    ← Domain service interfaces
│   └── IDomainService.cs               ← Marker interface
└── Share/
    ├── AggregateRoot.cs                 ← DO NOT MODIFY
    ├── DomainEntity.cs                  ← DO NOT MODIFY
    ├── DomainSchemas.cs                 ← ADD your schema constant here
    └── Sequences.cs                     ← ADD sequence name if needed
```

---

## Step-by-Step

### Step 1: Add Schema Constant

Edit `src/ApiEndpoints/Minimal.Domains/Share/DomainSchemas.cs`:

```csharp
public static class DomainSchemas
{
    public const string Migration = "migrate";
    public const string Profile = "pro";
    public const string {Feature} = "{prefix}";    // ← ADD THIS
}
```

### Step 2: Create the Entity Class

Create `src/ApiEndpoints/Minimal.Domains/Features/{Feature}/Entities/{Entity}.cs`:

```csharp
using Minimal.Domains.Share;

namespace Minimal.Domains.Features.{Feature}.Entities;

/// <summary>
/// {Description of the aggregate root}.
/// </summary>
public sealed class {Entity} : AggregateRoot
{
    #region Constructors

    /// <summary>
    /// Creates a new {Entity} with a system-assigned identity (base's <c>Guid.NewGuid()</c> overload).
    /// </summary>
    public {Entity}({constructor params for immutable + mutable fields}, string byUser)
        : base(byUser)
    {
        {ImmutableProp} = {value};
        {MutableProp} = {value};
    }

    /// <summary>
    /// Rehydrates an existing {Entity} with a known identity — used by static reference-data seeding only.
    /// Does not re-raise any creation event.
    /// </summary>
    internal {Entity}(Guid id, {all params}, string byUser)
        : base(id, byUser)
    {
        {ImmutableProp} = {value};
        {MutableProp} = {value};
    }

    #endregion

    #region Properties

    // Immutable properties (set only in constructor)
    public string {ImmutableProp} { get; private set; } = null!;

    // Mutable properties (changed via named mutation methods)
    public string? {MutableProp} { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Named, single-purpose mutation — prefer one method per state transition
    /// (e.g. <c>ChangeAmount</c>, <c>Cancel</c>) over a catch-all <c>Update</c>.
    /// </summary>
    public void Change{MutableProp}({type} {param}, string userId)
    {
        {MutableProp} = {param};

        SetUpdatedBy(userId);
    }

    #endregion
}
```

### Step 3: Create Owned Value Objects (if needed)

For complex nested types that don't have their own identity:

```csharp
namespace Minimal.Domains.Features.{Feature}.Entities;

/// <summary>
/// {Description} — owned value object, no independent identity.
/// </summary>
public class {OwnedType}
{
    public string {Prop1} { get; set; } = default!;
    public string? {Prop2} { get; set; }
}
```

### Step 4: Create Domain Service Interface (if needed)

If the entity needs external ID generation or cross-aggregate lookups:

```csharp
namespace Minimal.Domains.Services;

public interface I{Service} : IDomainService
{
    Task<string> NextValueAsync();
}
```

### Step 5: Add Sequence (if using auto-generated IDs)

Edit `src/ApiEndpoints/Minimal.Domains/Share/Sequences.cs` to add sequence name:

```csharp
public static class Sequences
{
    public const string {Entity}Seq = "{entity}_seq";
}
```

---

## Reference: PurchaseOrder (actual production code)

Every layer of this entity is hand-written — no declarative event/CRUD attribute is used anywhere on it (`Minimal.Domains/Features/ManualSample/Entities/PurchaseOrder.cs`):

```csharp
public sealed class PurchaseOrder : AggregateRoot
{
    public PurchaseOrder(string customerName, decimal amount, string byUser)
        : base(byUser)
    {
        CustomerName = customerName;
        Amount = amount;
        Status = PurchaseOrderStatus.Placed;

        AddEvent(new PurchaseOrderCreatedEvent(Id, CustomerName, Amount));
    }

    // Rehydration constructor — used by static reference-data seeding only, does NOT re-raise the event
    internal PurchaseOrder(Guid id, string customerName, decimal amount, string byUser)
        : base(id, byUser)
    {
        CustomerName = customerName;
        Amount = amount;
        Status = PurchaseOrderStatus.Placed;
    }

    public string CustomerName { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }

    public void ChangeAmount(decimal amount, string userId)
    {
        Amount = amount;
        SetUpdatedBy(userId);
    }

    public void Cancel(string userId)
    {
        Status = PurchaseOrderStatus.Cancelled;
        SetUpdatedBy(userId);
    }
}
```

Note the shape here: rather than one generic `Update(...)` method, `PurchaseOrder` exposes named, single-purpose mutation methods (`ChangeAmount`, `Cancel`) — each one is exactly the state transition it names. Prefer this over a catch-all `Update` when the mutations are conceptually distinct operations, as they are here.

---

## Validation Checklist

- [ ] Entity inherits from `AggregateRoot`, not using the `required` keyword on properties
- [ ] Properties use `{ get; private set; }` — no public setters
- [ ] Public constructor takes `string byUser` as last param; passes to `base(byUser)` (auto-generates the Id)
- [ ] Internal rehydration constructor (`base(id, byUser)`) exists only if the feature needs static seed data with fixed Ids
- [ ] Mutation methods are named per state transition (e.g. `ChangeAmount`, `Cancel`), not a single catch-all `Update`
- [ ] Every mutation method calls `SetUpdatedBy(userId)` at the end
- [ ] Immutable fields set only in constructor
- [ ] Schema constant added to `DomainSchemas.cs`
- [ ] Namespace follows `Minimal.Domains.Features.{Feature}.Entities`
- [ ] File placed in `src/ApiEndpoints/Minimal.Domains/Features/{Feature}/Entities/`
- [ ] Domain service interface extends `IDomainService` (if applicable)
- [ ] XML doc comments on class and all public members
- [ ] `dotnet build src/DKNet.Templates.sln -c Release` passes with zero warnings

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Using `required` keyword on properties | Use `{ get; private set; }` — values set in constructor |
| Public setters on properties | Make setters `private set` — mutate via methods only |
| Missing `SetUpdatedBy()` in a mutation method | Always call at end of every method that changes state |
| Using `DateTime.UtcNow` directly | Audit timestamps handled by `AuditedEntity` base class |
| Forgetting `internal` on the rehydration constructor | Mark it `internal` — only infra (e.g. static seed data) should call it |
| Assigning `Id` yourself in the public constructor | Don't — `base(byUser)` generates it via `Guid.NewGuid()` internally |

---

## Next Steps

After creating the domain entity, proceed to:
→ **dknet-efcore-config** skill to create the EF Core mapper configuration

For the judgment calls behind this entity's shape (aggregate boundary, entity vs. value object, invariant placement), see **dknet-ddd-principles**.
