---
name: dknet-domain-entity
description: Create DDD domain entities following this project's AggregateRoot/DomainEntity inheritance pattern. Use when adding a new domain entity or owned type to Minimal.Domains.
---

# Skill: Domain Entity Definition

Create domain entities that integrate with this project's DDD infrastructure — `AggregateRoot`, `DomainEntity`, and owned value objects.

If the aggregate boundary, entity-vs-value-object choice, or invariant placement isn't obvious, read **dknet-ddd-principles** first — this skill covers class mechanics, not those judgment calls.

---

## When to Use

- Adding a new aggregate root entity (e.g., `PurchaseOrder`, Invoice, Shipment)
- Adding a new owned value object (a plain class with no independent identity)
- Adding domain service interfaces for the new feature

> **Alternative — let the generator produce events/CRUD instead of hand-writing them.** Everything
> below teaches the hand-written shape (mirror `PurchaseOrder`). An entity can instead declare
> `[RaisesEvent(EventOperations.Created, ...)]` / `[RaisesEvent(EventOperations.Updated, ...)]` at
> the class level, a `[CrudCreate]` constructor, and `[CrudUpdate]` methods — the `DKNet.SlimBus.Generators`
> analyzer then produces the create/update request, handler, and route registration for you (see
> `Minimal.Domains/Features/AutomatedSample/Entities/Product.cs`). One real gap to know before
> picking that path: DataAnnotations on a generated request's properties (e.g. `[Range(0.01, double.MaxValue)]`
> on `Product.Price`) are forwarded but **not enforced** under this template's own routing convention —
> see `docs/samples/manual-vs-automated.md` for why.

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

- Entities are **NOT sealed** — they inherit from `AggregateRoot` (or `DomainEntity` for non-root entities)
- Properties use `{ get; private set; }` — mutation happens ONLY through named methods (e.g. `PurchaseOrder.ChangeAmount(...)`, `PurchaseOrder.Cancel(...)` — not a single generic `Update(...)`)
- The public constructor sets every field for a new entity and calls `base(byUser)`; a second `internal` constructor rehydrates a known identity (used by static seeding) and calls `base(id, byUser)`
- `SetCreatedBy(userId)` (via `base(byUser)`) and `SetUpdatedBy(userId)` are inherited from `AuditedEntity`/`AggregateRoot` — call `SetUpdatedBy` at the end of every mutation method
- Entity uses `AddEvent(...)` to publish domain events by hand (inherited from base) — or, as an alternative, declares `[RaisesEvent]` and lets DKNet's EF Core save hook raise it (see the callout above)
- The base `AggregateRoot(string byUser)` constructor auto-generates a new `Id` — you don't pass `Guid.Empty` yourself

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
    ├── DomainSchemas.cs                 ← optional named schema constant (see Step 1)
    └── Sequences.cs                     ← ADD sequence name if needed
```

---

## Step-by-Step

### Step 1: Pick a Schema

`DomainSchemas.cs` holds named constants (`Migration`, `Profile`) for reuse across mappers, but a
named constant isn't required — both current samples pass a literal schema string straight to
`ToTable(...)` in their EF Core mapper instead (`"manual_sample"` for `PurchaseOrder`, `"sample"`
for `Product` — see `dknet-efcore-config`). Add a constant to `DomainSchemas.cs` only if the schema
name is reused by more than one entity; otherwise a literal string is fine and is what both samples do.

### Step 2: Create the Entity Class

Create `src/ApiEndpoints/Minimal.Domains/Features/{Feature}/Entities/{Entity}.cs`. Mirror
`PurchaseOrder`'s shape: a public constructor for new entities (forwards to `base(byUser)`, which
auto-generates the `Id`), a second `internal` constructor for rehydrating a known identity (used by
static seeding only — forwards to `base(id, byUser)`), and named mutation methods instead of one
generic `Update(...)`:

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
    /// Creates a new {Entity}. Raises <see cref="{Entity}CreatedEvent"/>.
    /// </summary>
    public {Entity}({constructor params for immutable + mutable fields}, string byUser)
        : base(byUser)
    {
        {Prop1} = {param1};
        {Prop2} = {param2};

        AddEvent(new {Entity}CreatedEvent(Id, {Prop1}, {Prop2}));
    }

    /// <summary>
    /// Rehydrates a <see cref="{Entity}"/> with a known identity — used by static reference-data
    /// seeding only. Does not re-raise <see cref="{Entity}CreatedEvent"/>.
    /// </summary>
    internal {Entity}(Guid id, {all params}, string byUser)
        : base(id, byUser)
    {
        {Prop1} = {param1};
        {Prop2} = {param2};
    }

    private {Entity}()
    {
    }

    #endregion

    #region Properties

    public string {Prop1} { get; private set; } = null!;

    public decimal {Prop2} { get; private set; }

    #endregion

    #region Methods

    public void Change{Prop2}({type} value, string userId)
    {
        {Prop2} = value;
        SetUpdatedBy(userId);
    }

    /// <summary>
    /// Example of a guarded state transition — reject the call if the invariant it would
    /// establish already holds (see <c>PurchaseOrder.Cancel</c>'s "already cancelled" case,
    /// enforced one layer up in the command handler — see dknet-ddd-principles).
    /// </summary>
    public void {Transition}(string userId)
    {
        {StateProp} = {NewState};
        SetUpdatedBy(userId);
    }

    #endregion
}
```

This is a direct mirror of `Minimal.Domains/Features/ManualSample/Entities/PurchaseOrder.cs` — read
it alongside the template above; the real file is short (~55 lines) and shows every one of these
pieces with real names (`CustomerName`, `Amount`, `ChangeAmount`, `Cancel`, `PurchaseOrderStatus`).

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

```csharp
public enum PurchaseOrderStatus
{
    Draft,
    Placed,
    Cancelled
}

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

    // Rehydrates with a known identity — used by static reference-data seeding only.
    // Does not re-raise PurchaseOrderCreatedEvent.
    internal PurchaseOrder(Guid id, string customerName, decimal amount, string byUser)
        : base(id, byUser)
    {
        CustomerName = customerName;
        Amount = amount;
        Status = PurchaseOrderStatus.Placed;
    }

    private PurchaseOrder()
    {
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

Note what `PurchaseOrder` does *not* do that the generic template above shows for illustration: it
has no "ignore null/empty to preserve current value" update method — `ChangeAmount` and `Cancel` are
narrow, named, single-purpose mutations, which is the preferred shape when a mutation isn't a
generic partial-update. The "already cancelled" guard lives in the command handler, not on `Cancel`
itself (see `dknet-ddd-principles`).

### Alternative: Product's declarative shape

`Product` (`Minimal.Domains/Features/AutomatedSample/Entities/Product.cs`) reaches the same outcome
— a constructor, an update method, a created/updated event — without writing any of it by hand:

```csharp
[RaisesEvent(EventOperations.Created, Include = [nameof(Id), nameof(Name), nameof(Price)])]
[RaisesEvent(EventOperations.Updated, nameof(Price))]
public class Product : AggregateRoot
{
    [CrudCreate]
    public Product([Required, StringLength(150)] string name, [Range(0.01, double.MaxValue)] decimal price)
    {
        Name = name;
        Price = price;
    }

    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }

    [CrudUpdate]
    public void ChangePrice([Range(0.01, double.MaxValue)] decimal price) => Price = price;
}
```

`[CrudCreate]`/`[CrudUpdate]` generate the create/update request, handler, and route registration
(see `dknet-appservices-actions` and `dknet-endpoint-config`); `[RaisesEvent]` is raised by DKNet's
EF Core save hook, not by any line you write. **Important gap**: `Price`'s `[Range(0.01, double.MaxValue)]`
is genuinely forwarded onto the generated request but is never enforced under this template's own
endpoint-registration convention — confirmed live, `POST /v1/products` with a negative price returns
`201`, not `400`. See `docs/samples/manual-vs-automated.md` before picking this shape for an entity
whose validation needs to actually run.

---

## Validation Checklist

- [ ] Entity inherits from `AggregateRoot` (not using `required` keyword) — `sealed` is fine and is what `PurchaseOrder` does; leave it unsealed only if a later `[RaisesEvent]`/`[CrudCreate]` conversion is anticipated (`Product` is unsealed)
- [ ] Properties use `{ get; private set; }` — no public setters
- [ ] Public constructor's last param is `string byUser`; passes to `base(byUser)` (auto-generates `Id`)
- [ ] Internal rehydration constructor takes `Guid id` first, passes to `base(id, byUser)` — used by static seeding only
- [ ] Named mutation methods (e.g. `ChangeAmount`, `Cancel`) call `SetUpdatedBy(userId)` at the end — prefer these over one generic `Update(...)` unless the operation genuinely is a partial update of many fields
- [ ] Immutable fields set only in constructor
- [ ] Schema is either a `DomainSchemas` constant or an inline literal string passed to `ToTable(...)` (both samples use the inline literal — see `dknet-efcore-config`)
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
| Missing `SetUpdatedBy()` in a mutation method | Always call at end of every method that changes mutable state |
| Using `DateTime.UtcNow` directly | Audit timestamps handled by `AuditedEntity` base class |
| Forgetting `internal` on the rehydration constructor | Mark it `internal` — only infra (static seeding) should call it |
| Writing one generic `Update(...)` that silently no-ops on null/empty | Prefer named single-purpose methods (`ChangeAmount`, `Cancel`) — easier to test and to guard with a business rule in the handler |

---

## Next Steps

After creating the domain entity, proceed to:
→ **dknet-efcore-config** skill to create the EF Core mapper configuration

For the judgment calls behind this entity's shape (aggregate boundary, entity vs. value object, invariant placement), see **dknet-ddd-principles**.
