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
2. **Table name** (plural): e.g., `"CustomerProfiles"`, `"Orders"`
3. **Schema constant**: from `DomainSchemas` (e.g., `DomainSchemas.Profile`)
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
- Mapper is auto-discovered by `UseAutoConfigModel([typeof(CoreDbContext).Assembly])` in `InfraSetup.cs`

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

        // Table mapping with schema
        builder.ToTable("{TableName}", DomainSchemas.{Feature});
    }

    #endregion
}
```

### Step 2: Configure Owned Types (if entity has value objects)

If your entity has owned types like `Address` or `Company`, register them in
`src/ApiEndpoints/Minimal.Infra/Contexts/OwnedDataContext.cs`:

```csharp
// Inside OwnedDataContext, add to the existing ConfigureConventions or OnModelCreating:
builder.Entity<{Entity}>().OwnsOne(e => e.{OwnedProp}, owned =>
{
    owned.Property(p => p.{Prop}).HasMaxLength({max});
});
```

### Step 3: Add Static Seed Data (optional)

Create `src/ApiEndpoints/Minimal.Infra/Features/{Feature}/StaticData/{Entity}StaticData.cs`:

```csharp
using DKNet.EfCore.Extensions.DataSeeding;
using Minimal.Domains.Features.{Feature}.Entities;

namespace Minimal.Infra.Features.{Feature}.StaticData;

internal sealed class {Entity}StaticData : SqlDataSeeding<{Entity}>
{
    protected override IEnumerable<{Entity}> Data =>
    [
        new({params for seed row 1}),
        new({params for seed row 2}),
    ];
}
```

Auto-discovered by `UseAutoDataSeeding([typeof(InfraSetup).Assembly])`.

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

## Reference: CustomerProfile (actual production code)

### Mapper

```csharp
internal sealed class CustomerProfileConfigs : DefaultEntityTypeConfiguration<CustomerProfile>
{
    public override void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        base.Configure(builder);

        builder.HasIndex(p => p.Email).IsUnique();
        builder.HasIndex(p => p.MembershipNo).IsUnique();
        builder.Property(p => p.Avatar).HasMaxLength(50);
        builder.Property(p => p.BirthDay).HasColumnType("Date");
        builder.Property(p => p.Email).HasMaxLength(150).IsRequired();
        builder.Property(p => p.MembershipNo).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(50).IsRequired(false);
        builder.ToTable("CustomerProfiles", DomainSchemas.Profile);
    }
}
```

### Service Implementation

```csharp
internal sealed class MembershipService(ISequenceServices sequence) : IMembershipService
{
    public async Task<string> NextValueAsync()
    {
        var seq = await sequence.NextValueAsync(Sequences.MembershipSeq);
        return $"MEM-{seq:D6}";
    }
}
```

---

## Validation Checklist

- [ ] Mapper inherits from `DefaultEntityTypeConfiguration<{Entity}>` (not raw `IEntityTypeConfiguration`)
- [ ] Mapper is `internal sealed` (required for auto-discovery)
- [ ] `base.Configure(builder)` is called FIRST in Configure method
- [ ] All string properties have `HasMaxLength()`
- [ ] Required/optional correctly set with `IsRequired()` / `IsRequired(false)`
- [ ] Unique indexes added for business-key fields
- [ ] `ToTable("{Name}", DomainSchemas.{Schema})` set with correct schema
- [ ] File placed in `Minimal.Infra/Features/{Feature}/Mappers/`
- [ ] Service implementations are `internal sealed` in `.Services` namespace
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

---

## Next Steps

After configuring EF Core, proceed to:
→ **dknet-appservices-actions** skill to create CRUD actions and business logic
