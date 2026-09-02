---
name: dknet-efcore-config
description: Create EF Core entity type configurations (mappers), static data seeders, and infra services following this project's auto-discovery conventions. Use after creating a domain entity.
---

# Skill: EF Core Configuration

Create the persistence-layer configuration for a domain entity — mapper, static data, and infra service implementations.

---

## When to Use

- After creating a domain entity (dknet-domain-entity skill)
- Adding EF Core mapping configuration for a new entity
- Adding static seed data for lookup/reference tables
- Implementing domain service interfaces in the Infra layer

## Inputs Required

1. **Entity class** (from dknet-domain-entity): full class with properties
2. **Table name** (plural): e.g., `"PurchaseOrders"`, `"Products"`
3. **Schema**: a `DomainSchemas` constant, or an inline literal string (both samples in this template use a literal — `"manual_sample"` for `PurchaseOrder`, `"sample"` for `Product`)
4. **Unique indexes**: which fields need unique constraints?
5. **Column constraints**: max lengths, column types, required/optional
6. **Static seed data**: any initial data rows needed?
7. **Domain services to implement**: any `I{Service}` interfaces from Domains layer?

Note: this layer is hand-written the same way for both a hand-written entity and a `[CrudCreate]`/
`[CrudUpdate]`-declared one — no generator in this template touches `IEntityTypeConfiguration<T>`.
`ProductConfigs` (for the generator-driven `Product`) is just as hand-written as `PurchaseOrderConfigs`.

---

## Project Conventions (from actual codebase)

### Mapper Pattern

- Inherit from `DefaultEntityTypeConfiguration<TEntity>` (NOT raw `IEntityTypeConfiguration<T>`)
- Class must be `internal sealed` — Scrutor auto-discovery requires this
- Call `base.Configure(builder)` first — it configures base `AuditedEntity` fields (Id, CreatedBy, CreatedAt, etc.)
- Mapper is auto-discovered by `UseAutoConfigModel([typeof(CoreDbContext).Assembly])` in `InfraSetup.cs`

### File Locations

```
ApiEndpoints/Minimal.Infra/
├── Features/
│   └── {Feature}/
│       ├── Mappers/
│       │   └── {Entity}Configs.cs          ← Entity type configuration
│       ├── StaticData/
│       │   └── {Entity}StaticData.cs       ← Seed data (optional)
│       └── ExternalEvents/
│           └── {Event}Handler.cs           ← External event consumers (optional)
├── Services/
│   ├── {Service}.cs                         ← Domain service implementations
│   └── EventPublisher.cs                   ← DO NOT MODIFY
├── Contexts/
│   ├── CoreDbContext.cs                     ← DO NOT MODIFY
│   └── OwnedDataContext.cs                  ← Register owned types here
└── Extensions/
    ├── InfraSetup.cs                        ← DO NOT MODIFY (auto-scans)
    └── ServiceBusSetup.cs                   ← DO NOT MODIFY
```

### Auto-Discovery Rules (Scrutor)

Services are auto-registered when they meet ALL of:
- Class is `sealed`
- Namespace contains `.Repos` OR `.Services`
- Registered as scoped implementations of their interfaces

---

## Step-by-Step

### Step 1: Create the Mapper Class

Create `ApiEndpoints/Minimal.Infra/Features/{Feature}/Mappers/{Entity}Configs.cs`. Both samples
in this template hand-write this class the same way — nothing about `[CrudCreate]`/`[RaisesEvent]`
changes this layer (see `PurchaseOrderConfigs` and `ProductConfigs`):

```csharp
using Minimal.Domains.Features.{Feature}.Entities;

namespace Minimal.Infra.Features.{Feature}.Mappers;

internal sealed class {Entity}Configs : DefaultEntityTypeConfiguration<{Entity}>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        // MUST call base first — configures Id, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
        base.Configure(builder);

        // Unique indexes
        builder.HasIndex(p => p.{UniqueField}).IsUnique();

        // Property configurations
        builder.Property(p => p.{StringProp}).HasMaxLength({max}).IsRequired();
        builder.Property(p => p.{DecimalProp}).HasPrecision(18, 2);
        builder.Property(p => p.{OptionalProp}).HasMaxLength({max}).IsRequired(false);
        builder.Property(p => p.{DateProp}).HasColumnType("Date");

        // Table mapping — a DomainSchemas constant, or (as both samples do) an inline literal:
        builder.ToTable("{TableName}", "{schema}");
    }

    #endregion
}
```

`PurchaseOrderConfigs` maps to `ToTable("PurchaseOrders", "manual_sample")` with an index on
`CustomerName`, `Amount` at `HasPrecision(18, 2)`, and `Status` stored `HasConversion<string>()`.
`ProductConfigs` maps to `ToTable("Products", "sample")` with a **unique** index on `Name`
(`HasIndex(p => p.Name).IsUnique()`) and the same `HasPrecision(18, 2)` on `Price`.

### Step 2: Configure Owned Types (if entity has value objects)

If your entity has an owned value object (a plain class with no independent identity — see
`dknet-ddd-principles`), register it in `ApiEndpoints/Minimal.Infra/Contexts/OwnedDataContext.cs`:

```csharp
// Inside OwnedDataContext, add to the existing ConfigureConventions or OnModelCreating:
builder.Entity<{Entity}>().OwnsOne(e => e.{OwnedProp}, owned =>
{
    owned.Property(p => p.{Prop}).HasMaxLength({max});
});
```

Neither `PurchaseOrder` nor `Product` currently has an owned type — both samples are flat entities.

### Step 3: Add Static Seed Data (optional)

Create `ApiEndpoints/Minimal.Infra/Features/{Feature}/StaticData/{Entity}StaticData.cs`,
mirroring `PurchaseOrderStaticData` — inherit `DataSeedingConfiguration<TEntity>` and override
`GetDataAsync`, using the entity's `internal` rehydration constructor with fixed `Guid`s so seeding
is idempotent across re-runs:

```csharp
using Minimal.Domains.Features.{Feature}.Entities;

namespace Minimal.Infra.Features.{Feature}.StaticData;

internal sealed class {Entity}StaticData : DataSeedingConfiguration<{Entity}>
{
    protected override ValueTask<ICollection<{Entity}>> GetDataAsync(CancellationToken cancellation = new())
    {
        return ValueTask.FromResult<ICollection<{Entity}>>(
        [
            new {Entity}(new Guid("{fixed-guid-1}"), {seed field values}, SharedConsts.SystemAccount),
            new {Entity}(new Guid("{fixed-guid-2}"), {seed field values}, SharedConsts.SystemAccount)
        ]);
    }
}
```

`Product` has no static seed data — seeding is optional; skip this step for a feature that doesn't need reference rows.

**Critical wiring gotcha — a real bug this template hit once, already fixed:** `UseAutoConfigModel` +
`UseAutoDataSeeding` must be called in **both** places `CoreDbContext` gets built, or seeding silently
never appears over HTTP:

- `InfraSetup.AddInfraServices` — the DI-registered `CoreDbContext` the running app actually uses.
- `InfraMigration.MigrateDb` — a **separate** `CoreDbContext` built for the startup migration path.

`PurchaseOrderStaticData` was correctly discovered by the DI-path context but never appeared in a
real database, because `MigrateDb` built its own context without the same `.UseAutoDataSeeding(...)`
call — the migration ran, the seed rows never got inserted. Fixed by adding the identical call to
`InfraMigration.MigrateDb`. When adding new seed data, verify both call sites, not just one.

### Step 4: Implement Domain Service (if interface exists)

Create `ApiEndpoints/Minimal.Infra/Services/{Service}.cs`:

```csharp
using Minimal.Domains.Services;

namespace Minimal.Infra.Services;

/// <summary>
/// Implementation of <see cref="I{Service}"/>.
/// </summary>
internal sealed class {Service} : I{Service}
{
    private readonly ISequenceServices _sequence;

    public {Service}(ISequenceServices sequence)
    {
        _sequence = sequence;
    }

    public async Task<string> NextValueAsync()
    {
        var seq = await _sequence.NextValueAsync(Sequences.{Entity}Seq);
        return $"{PREFIX}-{seq:D6}";
    }
}
```

**Critical**: Class MUST be `sealed` and in the `Minimal.Infra.Services` namespace for Scrutor auto-registration.

### Step 5: Run Migration

```bash
cd ApiEndpoints
dotnet ef migrations add {MigrationName} -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj
```

Verify the generated migration in `ApiEndpoints/Minimal.Infra/Migrations/`.

---

## Reference: PurchaseOrder and Product (actual production code)

### Mappers (both hand-written)

```csharp
// Minimal.Infra/Features/ManualSample/Mappers/PurchaseOrderConfigs.cs
internal sealed class PurchaseOrderConfigs : DefaultEntityTypeConfiguration<PurchaseOrder>
{
    public override void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        base.Configure(builder);

        builder.HasIndex(p => p.CustomerName);
        builder.Property(p => p.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Status).HasConversion<string>();
        builder.ToTable("PurchaseOrders", "manual_sample");
    }
}

// Minimal.Infra/Features/AutomatedSample/Mappers/ProductConfigs.cs
internal sealed class ProductConfigs : DefaultEntityTypeConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(p => p.Name).IsUnique();
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.ToTable("Products", "sample");
    }
}
```

Note the two differences worth calling out: `PurchaseOrder`'s index on `CustomerName` is a plain
(non-unique) index for query performance, while `Product`'s index on `Name` is unique — a real
business constraint, not just an EF Core mapping habit. Also note `PurchaseOrder.Status` (an enum)
is stored `HasConversion<string>()` rather than the EF Core default (`int`) — readable in the raw
table without a lookup.

### Service Implementation

Neither sample implements a domain service — Step 4 above is the generic template for when a feature
needs one. `IMembershipService` / `SequenceService` / `Sequences` are pre-existing infra scaffolding
for exactly that pattern, wired by neither `PurchaseOrder` nor `Product`. The real implementation is a
one-line primary-constructor subclass, not a hand-written `NextValueAsync` body:

```csharp
// Minimal.Infra/Services/MembershipService.cs
internal sealed class MembershipService(CoreDbContext dbContext)
    : SequenceService(dbContext, Sequences.Membership), IMembershipService;
```

---

## Validation Checklist

- [ ] Mapper inherits from `DefaultEntityTypeConfiguration<{Entity}>` (not raw `IEntityTypeConfiguration`)
- [ ] Mapper is `internal sealed` (required for auto-discovery)
- [ ] `base.Configure(builder)` is called FIRST in Configure method
- [ ] All string properties have `HasMaxLength()`
- [ ] Required/optional correctly set with `IsRequired()` / `IsRequired(false)`
- [ ] Unique indexes added for business-key fields (see `Product.Name`) — a plain index is enough for a field that's just a query filter (see `PurchaseOrder.CustomerName`)
- [ ] `ToTable("{Name}", "{schema}")` set — a literal string (as both samples do) or a `DomainSchemas` constant, either is fine
- [ ] If adding `IDataSeedingConfiguration<T>`, `UseAutoDataSeeding` is wired into **both** `InfraSetup.AddInfraServices` and `InfraMigration.MigrateDb` — check both call sites, not just one
- [ ] File placed in `Minimal.Infra/Features/{Feature}/Mappers/`
- [ ] Service implementations are `internal sealed` in `.Services` namespace
- [ ] Migration generates cleanly: `dotnet ef migrations add {Name} -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj`
- [ ] `dotnet build -c Release` passes

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Inheriting `IEntityTypeConfiguration` directly | Use `DefaultEntityTypeConfiguration<T>` — it configures audit fields |
| Forgetting `base.Configure(builder)` | Must be first line — sets up Id, audit trail, IsDeleted filter |
| Making mapper `public` | Must be `internal sealed` for Scrutor auto-discovery |
| Placing mapper outside `Mappers/` folder | Scrutor scans by namespace — must be in correct folder |
| Missing `HasMaxLength` on strings | SQL Server defaults to `nvarchar(max)` — always constrain |
| Service not `sealed` or wrong namespace | Must be `sealed` + in `.Services` or `.Repos` namespace |
| Wiring `UseAutoDataSeeding` into only `InfraSetup.AddInfraServices` | Also wire it into `InfraMigration.MigrateDb` — that path builds its own `CoreDbContext`; seed data silently never appears over HTTP otherwise (the exact bug `PurchaseOrderStaticData` hit before this was fixed) |

---

## Next Steps

After configuring EF Core, proceed to:
→ **dknet-appservices-actions** skill to create CRUD actions and business logic
