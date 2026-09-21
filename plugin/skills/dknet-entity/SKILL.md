---
name: dknet-entity
description: Create DDD domain entities following this project's AggregateRoot/DomainEntity inheritance pattern, either hand-written (mode=manual) or generator-declared (mode=auto). Use when adding a new domain entity or owned type to <YourApp>.Domains. Invoke as `/dknet-entity <Feature> <Entity> [mode=manual|auto] [props…]` to scaffold it for a feature.
metadata:
  kind: workflow
  arguments: "<Feature> <Entity> [mode=manual|auto] [props…] e.g. Orders Order mode=manual Number:string Total:decimal"
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Agent
---

Usage: `/dknet-entity <Feature> <Entity> [mode=manual|auto] [props…] e.g. Orders Order mode=manual Number:string Total:decimal`

# Skill: Domain Entity Definition

Create domain entities that integrate with this project's DDD infrastructure — `AggregateRoot`,
`DomainEntity`, and (rarely) owned value objects — in either of the two shapes this template ships:
generator-declared (`mode=auto`, mirrors `AutomatedSample/Product`) or hand-written
(`mode=manual`, mirrors `ManualSample/PurchaseOrder`).

**`mode=auto` is the default.** Declare the CRUD surface with `[CrudCreate]`/`[CrudUpdate]`/
`[CrudAction]` and the events with `[RaisesEvent]`; hand-write an entity's operations only for the
reasons `dknet-feature-lifecycle` §1 lists. Within a hand-written entity, raise events in the same
order of preference: `[RaisesEvent]`, then `AddEvent<TEvent>()` (payload projected from the entity
by `IMapper`), then `AddEvent(new …)` with a hand-built payload.

If the aggregate boundary, entity-vs-value-object choice, or invariant placement isn't obvious, read
`dknet-ddd-principles` first — this skill covers class mechanics, not those judgment calls. Event
**consumers** (handlers that react to a raised event) are covered by `dknet-messaging-events`, not
here — this skill only covers how an entity raises an event.

## Hierarchy

```
AuditedEntity<Guid>   (DKNet.EfCore.Abstractions.Entities — Id, CreatedBy/On, UpdatedBy/On)
  └── DomainEntity     (<YourApp>.Domains/Share/DomainEntity.cs)
       └── AggregateRoot (<YourApp>.Domains/Share/AggregateRoot.cs)
```

`<YourApp>.Domains/Share/DomainEntity.cs`:

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

`<YourApp>.Domains/Share/AggregateRoot.cs`:

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

`<YourApp>.Domains/Features/ManualSample/Entities/PurchaseOrder.cs`:

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

    // Rehydrates with a known identity (static seeding only); does not re-raise PurchaseOrderCreatedEvent.
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

`<YourApp>.Domains/Features/ManualSample/Entities/PurchaseOrderCreatedEvent.cs` — the whole file:

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
  `public sealed record` next to the entity, in the same file or the same folder. This is the
  lowest rung — a hand-written entity can still carry `[RaisesEvent]`, or call
  `AddEvent<TEvent>()` and let `IMapper` project the payload; reach for a hand-built payload only
  when it is not a projection of the entity's own state.

## mode=auto — generator-declared (`AutomatedSample/Product`)

`<YourApp>.Domains/Features/AutomatedSample/Entities/Product.cs` (XML docs trimmed):

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

**`Product` is not `sealed`.** No architecture test enforces sealing on `<YourApp>.Domains` entities
(only on mappers/handlers/validators), and nothing subclasses it — an inconsistency, not a rule to
copy. Default to `sealed` unless you have a concrete reason not to, as `mode=manual` does.

### Events: `[RaisesEvent]`

Class-level, repeatable. This template uses only the label-less convention form:
`[RaisesEvent(operations, params string[] properties)]`, with `Include`/`Exclude` narrowing the
auto-composed payload. Two other forms exist in the package (type-naming, label) but neither sample
uses them.

- `EventOperations`: `Created`, `Updated`, `Deleted`.
- `Include`/`Exclude` (mutually exclusive) shape the auto-composed payload record —
  `Include = [nameof(Id), nameof(Name), nameof(Price)]` on the `Created` rule means
  `ProductCreatedEvent` carries exactly those three properties.
- The `string[] properties` argument narrows an `Updated` rule: non-empty raises only when a listed
  property's value actually changed (change-tracker original value, not whether the setter ran) —
  calling `ChangePrice` with its current price does not raise `ProductPriceUpdatedEvent`. Empty means
  any change qualifies. A property list on a `Created`-only rule is ignored with a build warning.
- **Composed name**: entity name + narrowing properties + operation + `Event`. On `Product`:
  `[RaisesEvent(EventOperations.Created, Include = [...])]` → `ProductCreatedEvent`;
  `[RaisesEvent(EventOperations.Updated, nameof(Price))]` → `ProductPriceUpdatedEvent`, **not**
  `ProductUpdatedEvent`; `[RaisesEvent(EventOperations.Updated, nameof(IsDiscontinued))]` →
  `ProductIsDiscontinuedUpdatedEvent`. This is what lets two `Updated` rules on one entity coexist.
- None of the three has a hand-written source file — they're compiled output. Verify a composed name
  against the built assembly before wiring a consumer to it:
  ```bash
  strings ApiEndpoints/<YourApp>.Domains/bin/Release/net10.0/<YourApp>.Domains.dll | grep Event
  ```
- `[RaisesEvent]` alone raises nothing — the host must register `DKNet.EfCore.Events`' save hook
  (`AddSlimBusEfCoreInterceptor<CoreDbContext>()`, already wired in this template) before declared
  events publish.
- Never two raise styles for the same logical change: one property's event comes from
  `[RaisesEvent]`, `AddEvent<TEvent>()`, or `AddEvent(new …)` — never a mix on one property. Prefer
  them in that order; see `dknet-ddd-principles` for when each rung is the right one. Writing a consumer for a
  raised event (either style) is covered by `dknet-messaging-events`, not this skill.

### Generator attributes: `[CrudCreate]`, `[CrudUpdate]`, `[CrudAction]`

All three live in `DKNet.EfCore.Abstractions.Attributes`. Mechanically identical: the annotated
member's parameter list *is* the generated request's payload, and its `DataAnnotations`
(`[Required]`, `[StringLength]`, `[Range]`) forward 1:1 onto the generated request property — but see
`dknet-crud`/`dknet-endpoint` for whether that attribute is actually **enforced** on the mapped route.

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

**Action vs. update** — decide by whether repeating the call is safe. `[CrudUpdate]`: caller supplies
a value and the row ends up holding it, calling twice is the same as once (`ChangePrice(decimal)`).
`[CrudAction]`: repetition isn't automatically safe (approve, discontinue, re-issue) — `POST` is the
default because it promises nothing about retries. `Product.Approve` was originally `[CrudUpdate]`,
over-promising retry-safety; moving it to `[CrudAction("approval")]` fixed the contract without
changing the method body. Drop a generated action for a hand-written route only when the operation
writes more than one aggregate in one transaction (`Product.Discontinue`, which also creates a
replacement product — see `dknet-crud`/`dknet-endpoint`).

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

`DKNet.EfCore.Abstractions.Attributes.SensitiveDataAttribute`, on a property. Two effects: unconditional
redaction in `DKNet.EfCore.AuditLogs` audit-log capture (not wired here), and role-gated JSON
serialization once the host opts in (`UseRoleAwareSensitiveData` — see `dknet-endpoint`).
`[SensitiveData("pricing")]` — only a caller in that role (any one of several named) gets the value
serialized; `[SensitiveData]` with no role — any *authenticated* caller gets it, unauthenticated is
refused. The attribute alone changes nothing until the serializer opts in. `DKNet.EfCore.DtoGenerator`
copies it onto the matching generated DTO property automatically — never re-declare it by hand.

### `IOwnedBy` — row-level isolation

Implement `IOwnedBy { string OwnedBy { get; } }` (`DKNet.EfCore.DataAuthorization`) to subject an
entity to the global read filter — a caller sees only rows whose `OwnedBy` matches their own
ownership key. `DataOwnerHook` stamps it on insert from `IDataOwnerProvider.GetOwnershipKey()` and
refuses reassignment to a key the caller doesn't hold. The mapper must still size the column by hand
— `builder.Property(p => p.OwnedBy).HasMaxLength(500).IsRequired();` (see `dknet-efcore-config`) —
the interface alone doesn't do it.

## Domain services, sequences, and `DomainSchemas`

`<YourApp>.Domains/Share/DomainSchemas.cs` holds named schema constants (`Migration = "migrate"`,
`Profile = "pro"`) for reuse across mappers — neither sample uses one; both pass a literal schema
string to `ToTable(...)` instead. Add a constant only when a schema name is reused by >1 entity.

`<YourApp>.Domains/Share/Sequences.cs` declares named PostgreSQL sequences:

```csharp
[SqlSequence]
public enum Sequences
{
    None = 0,

    [Sequence(typeof(int), FormatString = "T{DateTime:yyMMdd}{1:00000}", Max = 99999)]
    Membership = 1
}
```

The domain-service contract pattern that wraps a sequence (`<YourApp>.Domains/Services/`):

```csharp
public interface IDomainService;                                       // marker, no members
public interface ISequenceServices : IDomainService
{
    ValueTask<string> NextValueAsync();
}
public interface IMembershipService : ISequenceServices;                // one sequence, one interface
```

The `<YourApp>.Infra` implementation is a one-line primary-constructor subclass of an internal
`SequenceService` base (see `dknet-efcore-config`). Neither sample uses a sequence; add one the same
way `IMembershipService`/`MembershipService` do for `Sequences.Membership`, for entities needing a
human-readable sequential id instead of a `Guid`.

## Owned value objects

Not exercised by either sample — no `OwnsOne`/`OwnsMany` call exists anywhere in this solution. If a
feature genuinely needs one, the minimal pattern is a plain class with no independent identity,
configured in the entity's own mapper (`dknet-efcore-config`):

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

## Architecture tests that constrain `<YourApp>.Domains`

`Architecture/RecordArchitectureTests.cs` scans `<YourApp>.Domains`/`<YourApp>.AppServices` for any
record with a public property that has a **private** setter, and fails — Mapster/AutoMapper can't
map those. A hand-written event record (`public sealed record XCreatedEvent(...)`) is positional and
passes by construction. `Architecture/SampleInvariantTests.cs` enforces the two-sample split:
`ManualSample_ShouldNotUseAnyDeclarativeGenerationAttribute` fails on any `ManualSample` file
containing `[RaisesEvent`, `[CrudCreate]`, `[CrudUpdate]`, `[CrudAction`, or `[GenerateDto`;
`AutomatedSample_ShouldNotRaiseEventsByHand` fails on any `AutomatedSample` file calling `AddEvent(`.
No architecture test enforces `sealed`, visibility, or constructor shape on an entity directly — the
conventions above are followed by convention, not test (unlike mapper/handler/validator sealing).

## Unit tests to mirror

- `<YourApp>.App.Tests/Unit/ManualSample/PurchaseOrderTests.cs` — constructor sets properties and
  raises `PurchaseOrderCreatedEvent` (asserted via `order.GetEvents()`); the rehydration constructor
  does not raise it; mutation methods stamp `UpdatedBy`.
- `<YourApp>.App.Tests/Unit/AutomatedSample/ProductTests.cs` — constructor sets properties (no event
  assertion — declared events don't raise outside a real `SaveChanges`, see
  `dknet-messaging-events`); reflection asserts `[CrudCreate]`/`[CrudUpdate]` `DataAnnotations` are
  present, the only thing a unit test can prove about a forwarded-but-unenforced attribute;
  `Discontinue` called twice stays discontinued — the repeat-call refusal lives in the handler, not
  the entity method.

## Step-by-step

**mode=manual**

1. Create `ApiEndpoints/<YourApp>.Domains/Features/<Feature>/Entities/<Entity>.cs` following the
   three-constructor shape above, `AddEvent(new <Entity>CreatedEvent(...))` in the public one.
2. Add `public sealed record <Entity>CreatedEvent(...)` next to the entity.
3. Mark the class `sealed` unless you have a specific reason not to.
4. Continue to `dknet-efcore-config` for the mapper.

**mode=auto**

1. Create `ApiEndpoints/<YourApp>.Domains/Features/<Feature>/Entities/<Entity>.cs` following the
   attribute shape above (`[RaisesEvent]`, `[CrudCreate]`, `[CrudUpdate]`, `[CrudAction]`, `IOwnedBy`
   as needed).
2. Build once, inspect `obj/Generated/DKNet.SlimBus.Generators/…` for request/handler names and
   `strings …/<YourApp>.Domains.dll | grep Event` for composed event names.
3. Continue to `dknet-efcore-config` for the mapper (unchanged by this mode).

## mode=manual vs mode=auto — when to pick which

Full trade-off table lives in `dknet-feature-lifecycle`; short version: pick `mode=auto` for a plain
CRUD-shaped entity where built-in `Created`/`Updated`-on-change semantics and generated routes are
enough (a `FluentValidation` validator against a generated request still runs — see `dknet-crud`).
Pick `mode=manual` when construction/mutation needs logic beyond setting fields, an operation spans
more than one aggregate, or a domain method needs the acting user's identity as data.

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
| Assuming `ChangePrice`'s `[Range(0.01, double.MaxValue)]` is enforced because it's on the entity | It's forwarded onto the generated request but only *enforced* when the route is a literal `Map*(string, Delegate)` call — generated CRUD routes aren't. See `dknet-crud`. |
| Guessing a composed `[RaisesEvent]` name from the pattern alone | Always verify with `strings ApiEndpoints/<YourApp>.Domains/bin/Release/net10.0/<YourApp>.Domains.dll \| grep Event` after building — there's no source file to read. |
| Expecting an `Updated` rule to fire because a setter ran | It only fires when the named property's value actually changed relative to the change tracker's original value on that save. |
| Sealing `Product`-style logic because "the manual sample is sealed" | `sealed` is a good default but is not enforced on `<YourApp>.Domains` entities by any architecture test — don't cite one that doesn't exist. |

---

# Workflow: `/dknet-entity`

The procedure an agent follows when invoked with arguments. The reference sections above are the rules it applies.

You are scaffolding the **Domain + Infra** layers of a vertical slice. Stop after the migration is generated and the solution builds — `/dknet-crud` will continue from there.

### Inputs

`$ARGUMENTS` — feature folder (plural, PascalCase), aggregate name (singular, PascalCase), an optional
`mode=manual|auto` (defaults to `manual`), and an optional list of properties (`Name:Type`, append `?`
for nullable).

**The mode changes what this command writes onto the entity.** In `auto` the entity itself carries the
CRUD and event surface as attributes — skipping them here makes `/dknet-crud mode=auto` unreachable,
because the generator has nothing to read. If the mode was not supplied, apply
the `dknet-feature-lifecycle` skill §1 to choose one and say which you chose.

### Required reading

1. The reference sections above (mode=manual / mode=auto sections cover the exemplar shapes in full)
2. the `dknet-efcore-config` skill
3. `manual` exemplar — `ApiEndpoints/<YourApp>.Domains/Features/ManualSample/Entities/PurchaseOrder.cs`; `auto` exemplar — `ApiEndpoints/<YourApp>.Domains/Features/AutomatedSample/Entities/Product.cs`
4. `ApiEndpoints/<YourApp>.Infra/Features/ManualSample/Mappers/PurchaseOrderConfigs.cs` (exemplar mapper — hand-written in **both** modes; no generator produces this)

### Steps

1. Use the `dknet-implementer` subagent (via the Agent tool) to execute Steps 1–4 of the implementer protocol: domain entity, schema constant, owned types, EF Core mapper, optional sequence/seed data, then `dotnet ef migrations add <Name> -c CoreDbContext -p <YourApp>.Infra/<YourApp>.Infra.csproj` from `ApiEndpoints/`. Apply the mode=manual or mode=auto rules from the sections above verbatim — don't re-derive them.
2. Run `dotnet build -c Release` and stop on first error.
3. **`auto` only** — verify composed event-record names against the built DLL (see "Events: `[RaisesEvent]`" above) before wiring a consumer: `strings ApiEndpoints/<YourApp>.Domains/bin/Release/net10.0/<YourApp>.Domains.dll | grep <Entity>`.
4. Report:
   - mode used,
   - files created (relative paths),
   - migration name + tables/indexes,
   - for `auto`, the composed event-record names you verified,
   - the exact next command (`/dknet-crud <Feature> <Entity> mode=<mode>`).

### Constraints

- Do NOT touch `<YourApp>.AppServices` or `<YourApp>.Api` here — those belong to `/dknet-crud` and `/dknet-endpoint`.
- Entity/mapper/event rules: see Validation checklist above — verify against it, don't restate it.
