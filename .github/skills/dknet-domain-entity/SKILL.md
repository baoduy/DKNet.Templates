---
name: dknet-domain-entity
description: Create DDD domain entities following this project's AggregateRoot/DomainEntity inheritance pattern. Use when adding a new domain entity or owned type to Minimal.Domains.
---

# Skill: Domain Entity Definition

Create domain entities that integrate with this project's DDD infrastructure — `AggregateRoot`, `DomainEntity`, and owned value objects.

---

## When to Use

- Adding a new aggregate root entity (e.g., Order, Invoice, Product)
- Adding a new owned value object (e.g., Address, Company)
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

- Entities are **NOT sealed** — they inherit from `AggregateRoot` (or `DomainEntity` for non-root entities)
- Properties use `{ get; private set; }` — mutation happens ONLY through named methods
- Constructor sets immutable fields; `Update(...)` method handles mutable fields
- `SetCreatedBy(userId)` and `SetUpdatedBy(userId)` are inherited from `AuditedEntity`
- Entity uses `AddEvent(...)` to publish domain events (inherited from base)
- `Guid.Empty` for Id means "let the database generate it"

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
public class {Entity} : AggregateRoot
{
    #region Constructors

    /// <summary>
    /// Creates a new {Entity} with a system-assigned identity.
    /// </summary>
    public {Entity}(
        {constructor params for immutable + mutable fields},
        string byUser)
        : this(Guid.Empty, {forward all params}, byUser)
    {
    }

    /// <summary>
    /// Rehydrates an existing {Entity} from persistence.
    /// </summary>
    internal {Entity}(
        Guid id,
        {all params},
        string createdBy)
        : base(id, createdBy)
    {
        // Set immutable properties
        {ImmutableProp} = {value};

        // Delegate mutable fields to Update
        Update({mutable params}, createdBy);
    }

    #endregion

    #region Properties

    // Immutable properties (set only in constructor)
    public string {ImmutableProp} { get; private set; }

    // Mutable properties (changed via Update method)
    public string? {MutableProp} { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Updates mutable fields. Null/empty values are ignored (preserves current).
    /// </summary>
    public void Update({mutable params}, string userId)
    {
        if (!string.IsNullOrEmpty({param}))
        {
            {MutableProp} = {param};
        }

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

## Reference: CustomerProfile (actual production code)

```csharp
public class CustomerProfile : AggregateRoot
{
    // Constructor: new entity
    public CustomerProfile(string name, string membershipNo, string email, string phone, string byUser)
        : this(Guid.Empty, name, membershipNo, email, phone, byUser) { }

    // Constructor: rehydration
    internal CustomerProfile(Guid id, string name, string membershipNo, string email, string phone, string createdBy)
        : base(id, createdBy)
    {
        Name = name;
        Email = email;
        MembershipNo = membershipNo;
        Update(null, name, phone, null, createdBy);
    }

    // Immutable
    public string Email { get; private set; }
    public string MembershipNo { get; private set; }

    // Mutable
    public string Name { get; private set; }
    public string? Phone { get; private set; }
    public string? Avatar { get; private set; }
    public DateTime? BirthDay { get; private set; }

    public void Update(string? avatar, string? name, string? phoneNumber, DateTime? birthday, string userId)
    {
        Avatar = avatar;
        BirthDay = birthday;
        if (!string.IsNullOrEmpty(name)) Name = name;
        if (!string.IsNullOrEmpty(phoneNumber)) Phone = phoneNumber;
        SetUpdatedBy(userId);
    }
}
```

---

## Validation Checklist

- [ ] Entity inherits from `AggregateRoot` (not sealed, not using `required` keyword)
- [ ] Properties use `{ get; private set; }` — no public setters
- [ ] Constructor takes `string byUser` as last param; passes to `base(id, createdBy)`
- [ ] Public constructor uses `Guid.Empty` for new entities
- [ ] Internal constructor used for rehydration from persistence
- [ ] `Update(...)` method calls `SetUpdatedBy(userId)` at the end
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
| Making entity `sealed` | Remove `sealed` — entities inherit from `AggregateRoot` |
| Using `required` keyword on properties | Use `{ get; private set; }` — values set in constructor |
| Public setters on properties | Make setters `private set` — mutate via methods only |
| Missing `SetUpdatedBy()` in Update | Always call at end of mutation methods |
| Using `DateTime.UtcNow` directly | Audit timestamps handled by `AuditedEntity` base class |
| Forgetting `internal` on rehydration constructor | Mark it `internal` — only infra should call it |

---

## Next Steps

After creating the domain entity, proceed to:
→ **dknet-efcore-config** skill to create the EF Core mapper configuration
