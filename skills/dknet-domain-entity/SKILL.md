---
name: dknet-domain-entity
description: Create DDD domain entities following this project's AggregateRoot/DomainEntity inheritance pattern, either hand-written (mode=manual) or generator-declared (mode=auto). Use when adding a new domain entity or owned type to Minimal.Domains.
---

# Skill: Domain Entity Definition

Create domain entities that integrate with this project's DDD infrastructure — `AggregateRoot`,
`DomainEntity`, and (rarely) owned value objects — in either of the two shapes this template ships:
hand-written (`mode=manual`, mirrors `ManualSample/PurchaseOrder`) or generator-declared
(`mode=auto`, mirrors `AutomatedSample/Product`).

If the aggregate boundary, entity-vs-value-object choice, or invariant placement isn't obvious, read
`dknet-ddd-principles` first — this skill covers class mechanics, not those judgment calls. Event
**consumers** (handlers that react to a raised event) are covered by `dknet-messaging-events`, not
here — this skill only covers how an entity raises an event.

## Hierarchy

```
AuditedEntity<Guid>   (DKNet.EfCore.Abstractions.Entities — Id, CreatedBy/On, UpdatedBy/On)
  └── DomainEntity     (Minimal.Domains/Share/DomainEntity.cs)
       └── AggregateRoot (Minimal.Domains/Share/AggregateRoot.cs)
```

`Minimal.Domains/Share/DomainEntity.cs`:

```csharp
public abstract class DomainEntity : AuditedEntity<Guid>
{
    protected DomainEntity(Guid id, string createdBy, DateTimeOffset? createdOn = null) : base(id)
    {
        SetCreatedBy(createdBy, createdOn);
    }

    protected DomainEntity()
    {
    }
}
```

`Minimal.Domains/Share/AggregateRoot.cs`:

```csharp
public abstract class AggregateRoot : DomainEntity
{
    protected AggregateRoot(string createdBy, DateTimeOffset? createdOn = null)
        : this(Guid.NewGuid(), createdBy, createdOn)
    {
    }

    protected AggregateRoot(Guid id, string createdBy, DateTimeOffset? createdOn = null)
        : base(id, createdBy, createdOn)
    {
        SetCreatedBy(createdBy, createdOn);
    }

    protected AggregateRoot()
    {
    }
}
```

`AuditedEntity<Guid>` (`DKNet.EfCore.Abstractions`) supplies `Id`, `CreatedBy`, `CreatedOn`,
`UpdatedBy`, `UpdatedOn`, `LastModifiedBy`/`LastModifiedOn` (computed: `UpdatedBy ?? CreatedBy`, not
mapped columns), plus `protected SetCreatedBy(userName, createdOn?)` (no-op once `CreatedBy` is
already set) and `protected SetUpdatedBy(userName, updatedOn?)`. All four base classes are abstract
and every property setter is `private` — don't redeclare any of them.

**Two ways an aggregate ends up with an `Id`:**

- Call the `AggregateRoot(string createdBy)` overload (or `(Guid id, string createdBy)`) — this
  eagerly assigns `Id = Guid.NewGuid()` (or the given `id`) and stamps `CreatedBy` in memory, before
  `SaveChanges`. `PurchaseOrder`'s public constructor does this via `: base(byUser)`.
- Call neither — the parameterless `protected AggregateRoot()` runs, `Id` stays `Guid.Empty` and
  `CreatedBy` stays unset until `SaveChanges`. EF Core's mapper base class
  (`DefaultEntityTypeConfiguration<T>`) configures `Id` with `ValueGeneratedOnAdd().HasValueGenerator<GuidV7ValueGenerator>()`,
  so a fresh time-ordered GUID is assigned there instead, and the audit/data-ownership hooks stamp
  `CreatedBy`/`OwnedBy` from the authenticated caller in the same save (see `dknet-efcore-config` and
  the auditing rules below). `Product`'s `[CrudCreate]` constructor does this — it deliberately never
  calls `base(byUser)`, because that overload requires an acting-user string the generated request
  must not carry (see Acting user, below).

## mode=manual — hand-written (`ManualSample/PurchaseOrder`)

`Minimal.Domains/Features/ManualSample/Entities/PurchaseOrder.cs`:

```csharp
public enum PurchaseOrderStatus { Draft, Placed, Cancelled }

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

`Minimal.Domains/Features/ManualSample/Entities/PurchaseOrderCreatedEvent.cs` — the whole file:

```csharp
public sealed record PurchaseOrderCreatedEvent(Guid Id, string CustomerName, decimal Amount);
```

Rules this shape follows:

- **Three constructors.** A public one for a new entity (forwards to `base(byUser)`), an `internal`
  rehydration one taking `Guid id` first (forwards to `base(id, byUser)`, does **not** re-raise the
  created event), and a `private` parameterless one for EF Core materialization.
- `sealed` — `PurchaseOrder` is `sealed`, and sealed is the shape to default to; nothing about a
  hand-written entity needs it to be open for inheritance.
- Every property is `{ get; private set; }`. Mutation happens only through named methods
  (`ChangeAmount`, `Cancel`) — never a single generic `Update(...)`, and never a public setter.
- Every mutation method calls `SetUpdatedBy(userId)` at the end.
- The domain event is raised by hand: `AddEvent(new PurchaseOrderCreatedEvent(...))` inside the
  constructor, right where the aggregate becomes valid. The event itself is a plain
  `public sealed record` next to the entity, in the same file or the same folder.

## mode=auto — generator-declared (`AutomatedSample/Product`)

`Minimal.Domains/Features/AutomatedSample/Entities/Product.cs` (XML docs trimmed):

```csharp
[RaisesEvent(EventOperations.Created, Include = [nameof(Id), nameof(Name), nameof(Price)])]
[RaisesEvent(EventOperations.Updated, nameof(Price))]
[RaisesEvent(EventOperations.Updated, nameof(IsDiscontinued))]
public class Product : AggregateRoot, IOwnedBy
{
    [CrudCreate]
    public Product(
        [Required, StringLength(150)] string name,
        [Range(0.01, double.MaxValue)] decimal price,
        decimal? supplierCostPrice = null)
    {
        Name = name;
        Price = price;
        SupplierCostPrice = supplierCostPrice;
    }

    protected Product()
    {
    }

    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }
    public bool IsDiscontinued { get; private set; }

    // Stamped by DataOwnerHook; makes the row subject to the global read filter.
    public string OwnedBy { get; private set; } = string.Empty;

    [SensitiveData("pricing")] public decimal? SupplierCostPrice { get; private set; }
    [SensitiveData] public string? SupplierReferenceCode { get; private set; }

    [CrudUpdate]
    public void ChangePrice([Range(0.01, double.MaxValue)] decimal price) => Price = price;

    [CrudAction("approval")]
    public void Approve(string byUser) => SetUpdatedBy(byUser);

    [CrudAction(Verb = CrudActionVerb.Put)]
    public void Discontinue() => IsDiscontinued = true;

    [CrudAction("supplier-reference", Verb = CrudActionVerb.Put)]
    public void AssignSupplierReference([Required, StringLength(50)] string supplierReferenceCode) =>
        SupplierReferenceCode = supplierReferenceCode;
}
```

**`Product` is not `sealed`.** Neither an architecture test nor the generator requires this either
way — `Architecture/InfraTests.cs` and `Architecture/AppServiceTests.cs` enforce `internal sealed`
on mappers, handlers and validators, but nothing in `Architecture/*.cs` touches whether a
`Minimal.Domains` entity is sealed, and no generated file subclasses `Product`. Treat it as an
inconsistency to be aware of, not a rule to copy: default to `sealed` unless you have a concrete
reason not to, exactly as `mode=manual` does.

### Events: `[RaisesEvent]`

Class-level, repeatable. This template uses only the label-less convention form:
`[RaisesEvent(operations, params string[] properties)]`, with `Include`/`Exclude` as named
properties narrowing the auto-composed payload. Two other forms exist in the package
(type-naming — names an existing `[GenerateDto]` record; label — inserts a fixed word into the
composed name) but neither is used by either sample; don't mix a hand-raised `AddEvent` and a
`[RaisesEvent]` declaration for the same change.

- `EventOperations`: `Created`, `Updated`, `Deleted`.
- `Include`/`Exclude` (mutually exclusive, convention forms only) shape the auto-composed payload
  record. `Include = [nameof(Id), nameof(Name), nameof(Price)]` on the sample's `Created` rule means
  the generated `ProductCreatedEvent` carries exactly those three properties, nothing else.
- The `string[] properties` positional argument narrows an `Updated` rule: non-empty raises only
  when at least one listed property's value actually changed on that save (compared against EF
  Core's change-tracker original value, not whether the setter merely ran) — calling `ChangePrice`
  with the price it already holds does not raise `ProductPriceUpdatedEvent`. Empty means any change
  qualifies. A property list on a `Created`-only rule, or on a rule with no `Updated` flag, is
  ignored and reported as a build warning.
- **Composed name**: entity name + narrowing properties + operation + `Event`. On `Product`:
  `[RaisesEvent(EventOperations.Created, Include = [...])]` → `ProductCreatedEvent`;
  `[RaisesEvent(EventOperations.Updated, nameof(Price))]` → `ProductPriceUpdatedEvent`, **not**
  `ProductUpdatedEvent`; `[RaisesEvent(EventOperations.Updated, nameof(IsDiscontinued))]` →
  `ProductIsDiscontinuedUpdatedEvent`. This is what lets two `Updated` rules on one entity coexist.
- None of the three has a hand-written source file — they're compiled output. Verify a composed name
  against the built assembly before wiring a consumer to it:
  ```bash
  strings ApiEndpoints/Minimal.Domains/bin/Release/net10.0/Minimal.Domains.dll | grep Event
  ```
- `[RaisesEvent]` alone raises nothing — the host must register `DKNet.EfCore.Events`' save hook
  (`AddSlimBusEfCoreInterceptor<CoreDbContext>()`, already wired in this template) before declared
  events publish.
- Never both styles for the same logical change: pick `AddEvent` (mode=manual) or `[RaisesEvent]`
  (mode=auto) for a given entity's events, not a mix on one property. Writing a consumer for a
  raised event (either style) is covered by `dknet-messaging-events`, not this skill.

### Generator attributes: `[CrudCreate]`, `[CrudUpdate]`, `[CrudAction]`

All three live in `DKNet.EfCore.Abstractions.Attributes`. Mechanically identical: the annotated
member's parameter list *is* the generated request's payload, and `DataAnnotations` on each
parameter (`[Required]`, `[StringLength]`, `[Range]`) forward 1:1 onto the generated request's
matching property — but see `dknet-appservices-actions`/`dknet-endpoint-config` for whether that
forwarded attribute is actually **enforced** on the route the entity ends up mapped through.

| | `[CrudCreate]` | `[CrudUpdate]` | `[CrudAction]` |
|---|---|---|---|
| Placement | one constructor | a method | a method |
| Generates | `Create<Entity>Request` + handler | `Change<Member><Entity>Request` + handler, `PUT {id}` | `<Method><Entity>Request` + handler, `{Verb} {id}/{segment}` |
| Verb | `POST` (create route) | `PUT` | `POST` default, override to `Put`/`Patch` (`CrudActionVerb`) |
| Route segment | — | — | method name kebab-cased, or `[CrudAction("segment")]` |
| Response | `201` + DTO | `200` + DTO | `200` + DTO — never `204` |
| Acting-user param | never — see below | never | fine (see `Approve(string byUser)`), but see the pitfall below |

One `[CrudUpdate]` method changes exactly the fields in its own parameter list — a method needing to
change two independent fields together needs two `[CrudUpdate]` methods, not one bigger request.

**Action vs. update** — decide by whether repeating the call is safe. `[CrudUpdate]`: the caller
supplies a value and the row ends up holding it; calling twice is the same as once
(`ChangePrice(decimal)`). `[CrudAction]`: a business operation whose repetition is not automatically
safe (approve, discontinue, re-issue) — `POST` is the default precisely because it promises nothing
about retries. `Product.Approve` was originally `[CrudUpdate]`, over-promising retry-safety for an
approval; moving it to `[CrudAction("approval")]` fixed the contract without changing the method
body. The bar for dropping a generated action and hand-writing the route instead is higher still:
the operation writes more than one aggregate in one transaction (`Product.Discontinue`, which also
creates a replacement product — see `dknet-appservices-actions`/`dknet-endpoint-config` for the
hand-written replacement).

**`[CrudCreate]` never gets an acting-user parameter.** The generated create request's shape is the
constructor's parameter list, verbatim — a `createdBy`/`byUser` constructor parameter would become a
caller-settable body field. `Product`'s `[CrudCreate]` constructor takes only `name`, `price`,
`supplierCostPrice`; `CreatedBy`/`OwnedBy` are stamped at save time from the authenticated caller
instead (`DataOwnerHook`, `DKNet.EfCore.AuditLogs`' audit hook — see `dknet-efcore-config`).

**Pitfall — a `[CrudAction]` method's `byUser` parameter is caller-settable.** `Product.Approve(string
byUser)` becomes a generated `ApproveProductRequest` with a `required string ByUser` **body**
property — any caller can set it to any value; nothing populates it from the authenticated principal
the way `[FromClaim]` does for a hand-written request. Only use an action parameter named like an
acting-user field when that is genuinely intended as caller-supplied data, never to attribute the
call.

### `[SensitiveData]`

`DKNet.EfCore.Abstractions.Attributes.SensitiveDataAttribute`, on a property. Two independent
effects: unconditional redaction in `DKNet.EfCore.AuditLogs` audit-log capture (not wired by this
template today), and role-gated JSON serialization when the host opts in
(`UseRoleAwareSensitiveData` — see `dknet-endpoint-config` for where that's registered).
`[SensitiveData("pricing")]` — only a caller `IsInRole
("pricing")` (naming more than one role means any one is enough) gets the value in a serialized
response. `[SensitiveData]` with no role — any *authenticated* caller gets it; an unauthenticated
caller is still refused. Declaring the attribute changes nothing by itself; it only takes effect once
the host's serializer is opted in. `DKNet.EfCore.DtoGenerator` copies the attribute (roles and all)
onto the matching generated DTO property automatically — never re-declare it on a hand-written DTO
partial.

### `IOwnedBy` — row-level isolation

Implement `IOwnedBy { string OwnedBy { get; } }` (`DKNet.EfCore.DataAuthorization`) to make an
entity subject to the global read filter that shows a caller only rows whose `OwnedBy` matches their
own ownership key. `DataOwnerHook` stamps `OwnedBy` on insert from `IDataOwnerProvider
.GetOwnershipKey()` and refuses to let it be reassigned to a key the caller doesn't hold on update.
The mapper must still configure the column by hand — `builder.Property(p =>
p.OwnedBy).HasMaxLength(500).IsRequired();` (see `dknet-efcore-config`) — implementing the interface
alone does not size or require the column.

## Domain services, sequences, and `DomainSchemas`

`Minimal.Domains/Share/DomainSchemas.cs` holds named schema constants (`Migration = "migrate"`,
`Profile = "pro"`) for reuse across mappers — but neither sample uses one: both pass a literal
schema string straight to `ToTable(...)` (`"manual_sample"`, `"sample"`). Add a `DomainSchemas`
constant only when a schema name is reused by more than one entity.

`Minimal.Domains/Share/Sequences.cs` declares named PostgreSQL sequences:

```csharp
[SqlSequence]
public enum Sequences
{
    None = 0,

    [Sequence(typeof(int), FormatString = "T{DateTime:yyMMdd}{1:00000}", Max = 99999)]
    Membership = 1
}
```

The domain-service contract pattern that wraps a sequence (`Minimal.Domains/Services/`):

```csharp
public interface IDomainService;                                       // marker, no members
public interface ISequenceServices : IDomainService
{
    ValueTask<string> NextValueAsync();
}
public interface IMembershipService : ISequenceServices;                // one sequence, one interface
```

The `Minimal.Infra` implementation is a one-line primary-constructor subclass of an internal
`SequenceService` base — see `dknet-efcore-config`. Neither `PurchaseOrder` nor `Product` actually
uses a sequence; this exists for a future entity that needs a human-readable sequential identifier
instead of a `Guid`. Add a new `Sequences` member and a matching interface/implementation pair the
same way `IMembershipService`/`MembershipService` do for `Sequences.Membership`.

## Owned value objects

Not exercised by either shipped sample — `PurchaseOrder` and `Product` are both flat entities, and
no `OwnsOne`/`OwnsMany` call exists anywhere in this solution. If a feature genuinely needs
one, the minimal pattern is a plain class with no independent identity, configured in the entity's
own mapper (`dknet-efcore-config`):

```csharp
public sealed class Address
{
    public string Street { get; private set; } = null!;
    public string City { get; private set; } = null!;
}

// in the entity's IEntityTypeConfiguration<T>.Configure, after base.Configure(builder):
builder.OwnsOne(e => e.ShippingAddress, owned =>
{
    owned.Property(p => p.Street).HasMaxLength(200).IsRequired();
    owned.Property(p => p.City).HasMaxLength(100).IsRequired();
});
```

## Architecture tests that constrain `Minimal.Domains`

`Architecture/RecordArchitectureTests.cs` scans `Minimal.Domains` (and `Minimal.AppServices`) for any
record type with public properties that have a **private** setter, and fails — Mapster/AutoMapper
cannot map those. A hand-written event record (`public sealed record XCreatedEvent(...)`) is a
positional record and passes this by construction; don't add a private setter to one by hand.
`Architecture/SampleInvariantTests.cs` enforces the two-sample split at the source level:
`ManualSample_ShouldNotUseAnyDeclarativeGenerationAttribute` fails if any file under `ManualSample`
contains `[RaisesEvent`, `[CrudCreate]`, `[CrudUpdate]`, `[CrudAction`, or `[GenerateDto`;
`AutomatedSample_ShouldNotRaiseEventsByHand` fails if any file under `AutomatedSample` calls
`AddEvent(`. No architecture test in this repo enforces `sealed`, visibility, or constructor shape on
a `Minimal.Domains` entity directly — those constructor/property/method conventions above are
followed by both samples but are not test-enforced the way mapper/handler/validator sealing is
(`Architecture/InfraTests.cs`, `Architecture/AppServiceTests.cs`).

## Unit tests to mirror

- `Minimal.App.Tests/Unit/ManualSample/PurchaseOrderTests.cs` — constructor sets properties and
  raises `PurchaseOrderCreatedEvent` (asserted via `order.GetEvents()`); the rehydration constructor
  does not raise it; mutation methods stamp `UpdatedBy`.
- `Minimal.App.Tests/Unit/AutomatedSample/ProductTests.cs` — constructor sets properties (no event
  assertion here — declared events aren't raised outside a real `SaveChanges`, see
  `dknet-messaging-events`/integration tests for that); reflection asserts the `[CrudCreate]`
  constructor's and `[CrudUpdate]` method's `DataAnnotations` are actually present, since that's the
  only thing a unit test can prove about a forwarded-but-unenforced attribute; `Discontinue` called
  twice stays discontinued rather than throwing — the refusal on a repeat call lives one layer up, in
  the command handler, not on the entity method.

## Step-by-step

**mode=manual**

1. Create `ApiEndpoints/Minimal.Domains/Features/<Feature>/Entities/<Entity>.cs`: public constructor
   forwarding to `base(byUser)`, `internal` rehydration constructor forwarding to `base(id, byUser)`,
   private parameterless constructor, `{ get; private set; }` properties, named mutation methods
   calling `SetUpdatedBy`, `AddEvent(new <Entity>CreatedEvent(...))` in the public constructor.
2. Add `public sealed record <Entity>CreatedEvent(...)` next to the entity (own file or same file).
3. Mark the class `sealed` unless you have a specific reason not to.
4. Continue to `dknet-efcore-config` for the mapper.

**mode=auto**

1. Create `ApiEndpoints/Minimal.Domains/Features/<Feature>/Entities/<Entity>.cs`: class-level
   `[RaisesEvent(EventOperations.Created, Include = [...])]` (plus one `[RaisesEvent(EventOperations.Updated, nameof(Prop))]`
   per property that should raise its own event on change), `[CrudCreate]` on the constructor (no
   acting-user parameter), `{ get; private set; }` properties, `[CrudUpdate]` methods for
   caller-supplied value changes, `[CrudAction]` methods for named business operations, `IOwnedBy`
   if the feature needs row-level isolation.
2. Build the solution once, then inspect `obj/Generated/DKNet.SlimBus.Generators/…` for the request
   and handler names, and `strings …/Minimal.Domains.dll | grep Event` for the composed event names.
3. Continue to `dknet-efcore-config` for the mapper (unchanged by this mode).

## mode=manual vs mode=auto — when to pick which

Full trade-off table lives in `dknet-feature-lifecycle`; short version: pick `mode=auto` for a plain
CRUD-shaped entity where the built-in `Created`/`Updated`-on-change semantics and generated
create/update routes are enough, and where no rule beyond a `DataAnnotations` attribute needs to run
on the generated route (a `FluentValidation` validator against a generated request still runs — see
`dknet-appservices-actions`). Pick `mode=manual` when the entity's construction or mutation needs
logic beyond setting fields, when an operation spans more than one aggregate in one transaction, or
when a domain method needs the acting user's identity as data (not just an audit stamp).

## Validation checklist

- [ ] Entity ultimately derives from `AggregateRoot` (or `DomainEntity` for a non-root entity)
- [ ] Every property is `{ get; private set; }` — no public setters
- [ ] mode=manual: three constructors (public, `internal` rehydration, private parameterless);
      mode=auto: `[CrudCreate]` constructor with no acting-user parameter
- [ ] mode=manual: `AddEvent(...)` called in the constructor, event is a `public sealed record` next
      to the entity; mode=auto: `[RaisesEvent]` declared at class level, composed name verified via
      `strings` against the built DLL
- [ ] Every mutation method (mode=manual) or `[CrudUpdate]`/`[CrudAction]` method (mode=auto) that
      changes state ends by stamping the acting user (`SetUpdatedBy` for manual; auto's hooks do this
      at save time — don't call `SetUpdatedBy` yourself unless the method needs to record it as
      domain data, as `Approve` does)
- [ ] `IOwnedBy` implemented only when the feature needs row-level isolation, and the mapper sizes
      `OwnedBy` explicitly
- [ ] No mixing of `AddEvent` and `[RaisesEvent]` for the same entity
- [ ] `dotnet build -c Release` passes

## Common mistakes

| Mistake | Fix |
|---|---|
| Adding a `createdBy`/`byUser` constructor parameter to a `[CrudCreate]` constructor | Never — it becomes a caller-settable field on the generated create request. Stamp the acting user from the authenticated caller at save time instead. |
| Naming a `[CrudAction]` parameter `byUser` and expecting it to carry the authenticated caller | **What you might expect:** it's populated like `[FromClaim]`. **What actually happens:** it's an ordinary caller-settable body field on the generated request. **Why:** the generator forwards only the parameter list and its `DataAnnotations`; it has no concept of `[FromClaim]`. |
| Assuming `ChangePrice`'s `[Range(0.01, double.MaxValue)]` is enforced because it's on the entity | It's forwarded onto the generated request but only *enforced* when the route is a literal `Map*(string, Delegate)` call — generated CRUD routes aren't. See `dknet-appservices-actions`. |
| Guessing a composed `[RaisesEvent]` name from the pattern alone | Always verify with `strings ApiEndpoints/Minimal.Domains/bin/Release/net10.0/Minimal.Domains.dll \| grep Event` after building — there's no source file to read. |
| Expecting an `Updated` rule to fire because a setter ran | It only fires when the named property's value actually changed relative to the change tracker's original value on that save. |
| Sealing `Product`-style logic because "the manual sample is sealed" | `sealed` is a good default but is not enforced on `Minimal.Domains` entities by any architecture test — don't cite one that doesn't exist. |
