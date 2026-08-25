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
3. **Schema constant**: from `DomainSchemas` (e.g., `DomainSchemas.{Feature}`)
4. **Unique indexes**: which fields need unique constraints?
5. **Column constraints**: max lengths, column types, required/optional
6. **Static seed data**: any initial data rows needed?
7. **Domain services to implement**: any `I{Service}` interfaces from Domains layer?

---

## Project Conventions (from actual codebase)

### Mapper Pattern

- Inherit from `DefaultEntityTypeConfiguration<TEntity>` (NOT raw `IEntityTypeConfiguration<T>`)
- Class must be `internal sealed` — Scrutor auto-discovery requires this
- Call `base.Configure(builder)` first — it configures base `AuditedEntity` fields (Id, CreatedBy, CreatedAt, etc.)
- Mapper is auto-discovered by `UseAutoConfigModel([typeof(CoreDbContext).Assembly, ...])`
- `builder.ToTable("{TableName}", "{schema}")` takes a plain string schema — both current samples pass a literal schema name (`"manual_sample"` for `PurchaseOrder`, `"sample"` for `Product`) rather than routing through a shared constant. `DomainSchemas.cs` still exists if you want a named constant for a schema shared by several entities, but it isn't required.
- **This layer does not differ between the hand-written and generator-driven samples** — no generator in this template touches `IEntityTypeConfiguration`. `PurchaseOrderConfigs` and `ProductConfigs` are both plain hand-written mappers.

### File Locations

```
src/ApiEndpoints/Minimal.Infra/
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

Create `src/ApiEndpoints/Minimal.Infra/Features/{Feature}/Mappers/{Entity}Configs.cs`:

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
        builder.Property(p => p.{OptionalProp}).HasMaxLength({max}).IsRequired(false);
        builder.Property(p => p.{DateProp}).HasColumnType("Date");

        // Table mapping with schema — a literal string is fine (see PurchaseOrderConfigs/ProductConfigs);
        // reach for a DomainSchemas.cs constant only when several entities share one schema name.
        builder.ToTable("{TableName}", "{schema}");
    }

    #endregion
}
```

### Step 2: Configure Owned Types (if entity has value objects)

If your entity has an owned value object (see **dknet-domain-entity** "Step 3: Create Owned Value Objects"), register it in
`src/ApiEndpoints/Minimal.Infra/Contexts/OwnedDataContext.cs`:

```csharp
// Inside OwnedDataContext, add to the existing ConfigureConventions or OnModelCreating:
builder.Entity<{Entity}>().OwnsOne(e => e.{OwnedProp}, owned =>
{
    owned.Property(p => p.{Prop}).HasMaxLength({max});
});
```

### Step 3: Add Static Seed Data (optional)

Create `src/ApiEndpoints/Minimal.Infra/Features/{Feature}/StaticData/{Entity}StaticData.cs`. This mirrors `PurchaseOrderStaticData` — the only static seed data either sample carries; `Product` has none (a deliberate scope choice, not a generator limitation, see `docs/samples/manual-vs-automated.md`):

```csharp
using Minimal.Domains.Features.{Feature}.Entities;

namespace Minimal.Infra.Features.{Feature}.StaticData;

internal sealed class {Entity}StaticData : DataSeedingConfiguration<{Entity}>
{
    protected override ValueTask<ICollection<{Entity}>> GetDataAsync(CancellationToken cancellation = new())
    {
        return ValueTask.FromResult<ICollection<{Entity}>>(
        [
            new {Entity}(
                new Guid("{fixed-guid-1}"),
                {seed field values},
                SharedConsts.SystemAccount),
            new {Entity}(
                new Guid("{fixed-guid-2}"),
                {seed field values},
                SharedConsts.SystemAccount)
        ]);
    }
}
```

Seed rows use the entity's `internal` rehydration constructor (fixed `Guid`, no re-raised creation event) — see **dknet-domain-entity**. Auto-discovered by `UseAutoDataSeeding` — but only if it's wired into **both** places that build the model (see the gotcha below).

### The `UseAutoDataSeeding` gotcha: wire it into both host paths, or seeding silently never appears

This is a real bug this template hit once, already fixed — but it's easy to reintroduce for a new feature. `UseAutoConfigModel` + `UseAutoDataSeeding` must be called on **both**:

- `InfraSetup.AddInfraServices` — the DI-registered `CoreDbContext` the running app actually queries
- `InfraMigration.MigrateDb` — the separate `DbContext` built for the startup migration path

`PurchaseOrderStaticData` was originally wired only into `InfraSetup`, so migrations applied cleanly and the table existed, but no rows ever appeared over HTTP — the migration path's own model builder never called `UseAutoDataSeeding`, so nothing populated the table on the path that actually runs at startup. The fix was adding the identical `.UseAutoDataSeeding([typeof(InfraSetup).Assembly])` call to `InfraMigration.MigrateDb` too. If you add a new `{Entity}StaticData` and rows don't show up after `dotnet run`, check both files before suspecting the seeding class itself.

### Step 4: Implement Domain Service (if interface exists)

Create `src/ApiEndpoints/Minimal.Infra/Services/{Service}.cs`:

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
cd src/ApiEndpoints
./add-migration.sh {MigrationName}
```

Verify the generated migration in `src/ApiEndpoints/Minimal.Infra/Migrations/`.

---

## Reference: PurchaseOrderConfigs and ProductConfigs (actual production code)

Both mappers are plain hand-written `IEntityTypeConfiguration` classes — this layer is identical in shape whether the entity's events/CRUD are hand-written (`PurchaseOrder`) or generator-driven (`Product`):

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
```

```csharp
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

Note `PurchaseOrder.Status` (an enum) is stored as a string via `.HasConversion<string>()` — reach for that whenever an entity has an enum property you want readable in the database rather than stored as its underlying `int`.

Neither sample implements a domain service (no external ID generator or cross-aggregate lookup is needed by either) — Step 4 above is a generic template for when a future feature does need one. `IMembershipService`/`ISequenceServices`/`Sequences.cs` are pre-existing scaffolding for exactly that pattern; they're unused by both current samples, not something either one wires up.

---

## Validation Checklist

- [ ] Mapper inherits from `DefaultEntityTypeConfiguration<{Entity}>` (not raw `IEntityTypeConfiguration`)
- [ ] Mapper is `internal sealed` (required for auto-discovery)
- [ ] `base.Configure(builder)` is called FIRST in Configure method
- [ ] All string properties have `HasMaxLength()`
- [ ] Required/optional correctly set with `IsRequired()` / `IsRequired(false)`
- [ ] Unique indexes added for business-key fields
- [ ] `ToTable("{Name}", "{schema}")` set with a schema name
- [ ] File placed in `Minimal.Infra/Features/{Feature}/Mappers/`
- [ ] Service implementations are `internal sealed` in `.Services` namespace
- [ ] If seed data is added, `UseAutoDataSeeding` is wired into **both** `InfraSetup.AddInfraServices` and `InfraMigration.MigrateDb`
- [ ] Migration generates cleanly: `./add-migration.sh {Name}`
- [ ] `dotnet build src/DKNet.Templates.sln -c Release` passes

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
| Wiring `UseAutoDataSeeding` into only `InfraSetup` | Also add it to `InfraMigration.MigrateDb` — otherwise seed rows exist after migration but never appear over HTTP (this exact bug already happened once with `PurchaseOrderStaticData`) |

---

## Next Steps

After configuring EF Core, proceed to:
→ **dknet-appservices-actions** skill to create CRUD actions and business logic
