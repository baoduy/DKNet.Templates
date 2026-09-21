---
name: dknet-efcore-config
description: Create EF Core entity type configurations (mappers), static data seeders, CoreDbContext wiring, and infra domain-service implementations following this project's assembly-scan auto-discovery conventions. Use after creating a domain entity, for both mode=manual and mode=auto entities.
---

# Skill: EF Core Configuration

Create the persistence-layer configuration for a domain entity — mapper, optional static seed data,
and (rarely) an infra domain-service implementation. This layer is **hand-written identically for
both entity shapes** (`dknet-domain-entity`'s `mode=manual`/`mode=auto`): no generator in this
template — not `[RaisesEvent]`, not `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`, not
`[GenerateDto]` — touches `IEntityTypeConfiguration<T>`. `ProductConfigs` (for the generator-driven
`Product`) is exactly as hand-written as `PurchaseOrderConfigs`.

## Mapper

`Minimal.Infra/Features/ManualSample/Mappers/PurchaseOrderConfigs.cs` — the whole file:

```csharp
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

`Minimal.Infra/Features/AutomatedSample/Mappers/ProductConfigs.cs` — the whole file:

```csharp
internal sealed class ProductConfigs : DefaultEntityTypeConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(p => p.Name).IsUnique();
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.OwnedBy).HasMaxLength(500).IsRequired();
        builder.Property(p => p.SupplierCostPrice).HasPrecision(18, 2);
        builder.Property(p => p.SupplierReferenceCode).HasMaxLength(50);
        builder.ToTable("Products", "sample");
    }
}
```

Location: `Minimal.Infra/Features/<Feature>/Mappers/<Entity>Configs.cs`. Discovered by
`UseAutoConfigModel([typeof(CoreDbContext).Assembly, typeof(Sequences).Assembly])` — an **assembly
scan for every `IEntityTypeConfiguration<T>`**, not a Scrutor convention scan (see Infra services,
below, for what Scrutor actually is and isn't used for here). This call is wired in **both**
`InfraSetup.AddInfraServices` and `InfraMigration.MigrateDb`.

**Always call `base.Configure(builder)` first.** `DefaultEntityTypeConfiguration<TEntity>`
(`DKNet.EfCore.Extensions`) does three things when it applies:

- If an `Id` property exists: `HasKey("Id")`, plus a value generator — `ValueGeneratedOnAdd()` for a
  numeric key, or `ValueGeneratedOnAdd().HasValueGenerator<GuidV7ValueGenerator>()` for a `Guid` key
  (a time-ordered GUID assigned at insert, distinct from the plain `Guid.NewGuid()` an
  `AggregateRoot(string createdBy)` constructor assigns eagerly in memory — see
  `dknet-domain-entity`).
- If the entity implements `IAuditedProperties`: `CreatedBy`/`CreatedOn` required, `CreatedBy`
  max length 255, both locked against change after insert (`SetAfterSaveBehavior`); `UpdatedBy` max
  length 255, nullable; `UpdatedOn` nullable.
- If the entity implements `IConcurrencyEntity<T>`: a `RowVersion` concurrency token,
  `ValueGeneratedOnAddOrUpdate()`. Neither sample uses this.

There is no soft-delete / `IsDeleted` convention anywhere in this base class or in either sample —
don't assume one exists.

Beyond `base.Configure`, everything is ordinary EF Core Fluent API: `HasIndex` (plain, or
`.IsUnique()` for a real business constraint — `Product.Name`'s uniqueness is enforced by the
database index, not the `CreateProductRequestValidator`, which only *checks* first — two concurrent
callers can still both pass the check), `HasMaxLength`/`HasPrecision` on every column, and
`ToTable("Name", "schema")` with a literal schema string or a `DomainSchemas` constant (both samples
use a literal). Store every mapped enum with `HasConversion<string>()` —
`InfraTests.AllEnumProperties_StoringToDb_ShouldHaveStringConversion` enforces this for every enum
property in the model, and `PurchaseOrder.Status` is the shipped example. Give every mapped `string`
an explicit `HasMaxLength` — `InfraTests.NoEntityString_ShouldBe_ConfiguredAs_Max` fails on any
unconstrained string, which on PostgreSQL would otherwise map to unbounded `text`.

**Owned types (`OwnsOne`/`OwnsMany`).** Not exercised by either shipped sample — no `OwnsOne` call
exists anywhere under `ApiEndpoints`. Configure one inside the owning entity's own
`Configure(builder)`, after `base.Configure(builder)`, the same as any other Fluent API call:

```csharp
builder.OwnsOne(e => e.ShippingAddress, owned =>
{
    owned.Property(p => p.Street).HasMaxLength(200).IsRequired();
    owned.Property(p => p.City).HasMaxLength(100).IsRequired();
});
```

There is no separate `OwnedDataContext` or similar file in this template — an owned type is
configured where its owner is, not in a shared file.

## Static data seeding

`Minimal.Infra/Features/ManualSample/StaticData/PurchaseOrderStaticData.cs` — the whole file:

```csharp
internal sealed class PurchaseOrderStaticData : DataSeedingConfiguration<PurchaseOrder>
{
    protected override ValueTask<ICollection<PurchaseOrder>> GetDataAsync(
        CancellationToken cancellation = new())
    {
        return ValueTask.FromResult<ICollection<PurchaseOrder>>(
        [
            new PurchaseOrder(
                new Guid("6E6F4D3C-1B7E-4C7A-9F1D-8A2B5C6D7E01"),
                "Acme Pte Ltd", 1250.00m, SharedConsts.SystemAccount),
            new PurchaseOrder(
                new Guid("6E6F4D3C-1B7E-4C7A-9F1D-8A2B5C6D7E02"),
                "Globex Corporation", 875.50m, SharedConsts.SystemAccount),
            new PurchaseOrder(
                new Guid("6E6F4D3C-1B7E-4C7A-9F1D-8A2B5C6D7E03"),
                "Initech LLC", 430.25m, SharedConsts.SystemAccount)
        ]);
    }
}
```

Location: `Minimal.Infra/Features/<Feature>/StaticData/<Entity>StaticData.cs`. Inherit the
**base class** `DataSeedingConfiguration<T>` (`DKNet.EfCore.Extensions.Configurations`) — not an
`IDataSeedingConfiguration<T>` interface. Override the `protected` `GetDataAsync(CancellationToken)`
and return the fixed rows via the entity's `internal` rehydration constructor (known `Guid`s,
`SharedConsts.SystemAccount` as `createdBy`) — never the public constructor, so seeding never
re-raises the entity's created event. Class must be `internal sealed`
(`InfraTests.AllSeedingDataClassesShouldBeInternalAndSealed`, checked against
`IDataSeedingConfiguration` implementers).

**Re-run semantics** (from `DataSeedingConfiguration<T>`'s own doc comments): the base class's
`GetMissingAsync` filters the rows `GetDataAsync` returns down to the ones **not already present by
primary key** before inserting — comparing key columns read from the database, not entity equality
(a plain reference type has no `Equals` override, so entity-equality comparison would never match
freshly materialized rows and would re-insert every candidate on every run). This means seeding is
**insert-if-missing only**: it inserts a row whose fixed `Guid` isn't in the table yet, and never
updates a row that's already there, even if `GetDataAsync`'s in-code values changed since the row was
first seeded.

**Discovery and the two-call-site rule.** `UseAutoDataSeeding([typeof(InfraSetup).Assembly])` scans
for every `IDataSeedingConfiguration` implementer by assembly scan (same style as
`UseAutoConfigModel`, not Scrutor) and must be called in **both**:

- `Minimal.Infra/Extensions/InfraSetup.cs` → `AddInfraServices` (the DI-registered `CoreDbContext`
  the running app uses)
- `Minimal.Infra/Extensions/InfraMigration.cs` → `MigrateDb` (a **separate** `CoreDbContext` built
  for the startup-migration path; seeding runs as part of `db.Database.MigrateAsync()`)

This is a real bug the template hit once already: `PurchaseOrderStaticData` was correctly discovered
by the DI-path context but the migration path built its own context without the same
`.UseAutoDataSeeding(...)` call, so seed rows never appeared in a real database even though the
migration itself ran. When adding new seed data, verify both call sites, not just one.

**No test fixture wires this.** Neither `Minimal.App.Tests`' `ApiFixture` nor
`Minimal.App.BDDTests`' `BddApiFactory` calls `.UseAutoDataSeeding(...)` on their in-memory
`DbContext` — that wiring exists only in the two real composition-root call sites above. Tests that
actually exercise seeded data:

- `Minimal.App.Tests/Unit/ManualSample/PurchaseOrderStaticDataTests.cs` — invokes the `protected
  GetDataAsync` via reflection (nothing public exposes it for a direct call) and asserts the three
  fixed rows, owned by `SharedConsts.SystemAccount`, with distinct `Id`s.
- `Minimal.App.Tests/Integration/ManualSample/V1/InfraMigrationSeedingTests.cs` — runs
  `InfraMigration.MigrateDb` itself against a real, ephemeral Postgres `Testcontainers` instance,
  then reads `/v1/purchase-orders` over HTTP. This is the one test that fails if
  `.UseAutoDataSeeding(...)` is ever removed from `InfraMigration.MigrateDb`.

## `CoreDbContext`

`Minimal.Infra/Contexts/CoreDbContext.cs` — `internal class CoreDbContext(DbContextOptions options,
IEnumerable<IDataOwnerProvider>? dataKeyProviders = null) : DbContext(options), IDataOwnerDbContext`.
No `DbSet<T>` declarations anywhere — the model is built entirely from the `IEntityTypeConfiguration<T>`
scan. It exposes `AccessibleKeys` from the first registered `IDataOwnerProvider`
(`DKNet.EfCore.DataAuthorization` uses this for the global read filter on any `IOwnedBy` entity), and
overrides every `SaveChanges`/`SaveChangesAsync` entry point to call `EnsureOwnershipResolvable()`
first:

```csharp
private void EnsureOwnershipResolvable()
{
    if (_dataKeyProvider is null) return;
    if (!string.IsNullOrEmpty(_dataKeyProvider.GetOwnershipKey())) return;

    var hasUnattributableInsert = ChangeTracker.Entries()
        .Any(e => e.State == EntityState.Added
                  && e.Entity is IAuditedProperties { CreatedBy: null or "" }
                  && e.Metadata.FindProperty(nameof(IAuditedProperties.CreatedBy)) is { IsNullable: false });

    if (hasUnattributableInsert) throw new OwnershipRequiredException();
}
```

Fails closed, before EF Core attempts the insert, when the ownership key can't be resolved and a new
row would be left with no `CreatedBy` — otherwise EF Core's own required-property check throws a raw
`DbUpdateException` that leaks column/entity names into the response. Mapped to `403 Forbidden` by
the `StatusCode` branch of `AddErrorResponses(...)` (see `dknet-endpoint-config`), not `500`.

`InfraSetup.AddInfraServices` — the DI wiring, in full:

```csharp
public static IServiceCollection AddInfraServices(this IServiceCollection service)
{
    service
        .AddScoped<IMembershipService, MembershipService>()
        .AddSpecRepo<CoreDbContext>()
        .AddEventPublisher<CoreDbContext, EventPublisher>()
        .AddDbContextWithHook<CoreDbContext>((sp, builder) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var conn = config.GetConnectionString(SharedConsts.DbConnectionString)!;

            builder.UseNpgsqlWithMigration(conn)
                .UseAutoConfigModel([typeof(CoreDbContext).Assembly, typeof(Sequences).Assembly])
                .UseAutoDataSeeding([typeof(InfraSetup).Assembly]);
        });

    return service;
}

internal static DbContextOptionsBuilder UseNpgsqlWithMigration(
    this DbContextOptionsBuilder builder, string connectionString) =>
    builder.UseNpgsql(connectionString, o => o
        .MinBatchSize(1)
        .MaxBatchSize(100)
        .MigrationsHistoryTable(nameof(CoreDbContext), DomainSchemas.Migration)   // table "CoreDbContext", schema "migrate"
        .MigrationsAssembly(typeof(CoreDbContext).Assembly)
        .EnableRetryOnFailure()
        .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
```

`Minimal.Infra/Extensions/InfraMigration.cs` — the startup-migration path, in full:

```csharp
public static async Task MigrateDb(string connectionString)
{
    await using var db = new CoreDbContext(
        new DbContextOptionsBuilder<CoreDbContext>()
            .UseAutoConfigModel([typeof(CoreDbContext).Assembly, typeof(Sequences).Assembly])
            .UseNpgsqlWithMigration(connectionString)
            .UseAutoDataSeeding([typeof(InfraSetup).Assembly])
            .Options);

    // Seeding runs as part of MigrateAsync via UseAutoDataSeeding above.
    await db.Database.MigrateAsync();
}
```

`AddSpecRepo<CoreDbContext>()` wires `IRepositorySpec` (see `dknet-queries-specs`).
`AddEventPublisher<CoreDbContext, EventPublisher>()` wires `Minimal.Infra/Services/EventPublisher.cs`
— a `DefaultEventPublisher` override that does `bus.Publish(eventObj)` — as the sink both raise
styles (`AddEvent`/`[RaisesEvent]`) publish through after a successful save.

## Migrations

Always from the solution's `ApiEndpoints/` directory (there is no wrapper script — inline the real
`dotnet ef` command):

```bash
dotnet ef migrations add <Name> -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj
dotnet ef migrations remove -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj
```

Inspect the generated migration under `Minimal.Infra/Migrations/` before continuing — confirm the
table, columns, and any index/constraint match what the mapper declares. Never edit an
already-applied migration; add a new one instead. `Architecture/MigrationSchemaTests.cs` pins
specific facts about the compiled model: `Product.Name` has a **unique** single-column index,
the model declares a sequence in schema `seq` and a sequence named `Seq_Membership` (never one named
after `Sequences.None`), the provider is not SQL Server, and both `Minimal.AppHost` and
`Minimal.Infra` reference PostgreSQL/Npgsql packages, never SQL Server ones. At application start,
`FeatureManagement:RunDbMigrationWhenAppStart` (or the `migration` launch argument — `dotnet run
--project ApiEndpoints/Minimal.Api -- migration`) is what actually calls this path; removing a
feature's tables is a **drop migration**, covered by `dknet-feature-lifecycle`, not here.

## Infra domain services

`Minimal.Domains/Services/` holds the interface, `Minimal.Infra/Services/` the implementation,
`InfraSetup.AddInfraServices` the registration — **by explicit `AddScoped<TInterface,
TImplementation>()`, one line per service.** There is no Scrutor convention scan anywhere in this
template's `Minimal.Infra` registration path — the only `Scan(` call in the whole solution is
Mapster's `TypeAdapterConfig.Scan` in `Minimal.AppServices/Extensions/MapsToExtensions.cs`, unrelated
to service registration. Do not rely on a naming convention or namespace
(`.Services`/`.Repos`) to get a service registered — add the `AddScoped` line yourself. `internal
sealed` is still required by the same architecture rule that covers mappers/handlers/validators
whenever the class implements a handler-shaped interface (`InfraTests.AllHandlerClassesShouldBeInternalAndSealed`
covers `IRequestHandler<>`/`IConsumer<>` implementers specifically — a plain domain-service
implementation like `MembershipService` isn't targeted by that rule either, but follow the same
`internal sealed` convention regardless).

```csharp
// Minimal.Infra/Services/SequenceService.cs — internal abstract base, one per sequence-backed service
internal abstract class SequenceService(DbContext dbContext, Sequences sequence) : ISequenceServices
{
    public virtual async ValueTask<string> NextValueAsync() =>
        dbContext.IsNpgsql()
            ? await dbContext.NextSeqValueWithFormat(sequence)
            : Guid.NewGuid().ToString();
}

// Minimal.Infra/Services/MembershipService.cs — the whole file
internal sealed class MembershipService(CoreDbContext dbContext)
    : SequenceService(dbContext, Sequences.Membership), IMembershipService;
```

`NextValueAsync()` formats the next value with the sequence's `FormatString` on PostgreSQL
(`NextSeqValueWithFormat`), and falls back to a plain `Guid` when the context isn't Npgsql — so a
unit test against an in-memory/SQLite context never needs a real Postgres sequence. Add your own
service the same way: an interface in `Minimal.Domains/Services/`, an `internal sealed`
implementation in `Minimal.Infra/Services/`, one `AddScoped` line in `AddInfraServices`.

## Architecture tests constraining `Minimal.Infra`

`Architecture/InfraTests.cs`:

- `AllEfConfigClassesShouldBeInternalAndSealed` — every `IEntityTypeConfiguration<T>` implementer.
- `AllSeedingDataClassesShouldBeInternalAndSealed` — every `IDataSeedingConfiguration` implementer.
- `AllHandlerClassesShouldBeInternalAndSealed` — every `IRequestHandler<>`/`IRequestHandler<,>`/
  `IConsumer<>` implementer (covers `Minimal.Infra/Features/*/ExternalEvents/*` consumers).
- `AllValidatorClassesShouldBeInternalAndSealed` — every `AbstractValidator<T>` subclass found in
  `Minimal.Infra` (none shipped there today; validators live in `Minimal.AppServices`).
- `AllEnumProperties_StoringToDb_ShouldHaveStringConversion` — every enum property in the built
  model must have `ProviderClrType == typeof(string)`.
- `NoEntityString_ShouldBe_ConfiguredAs_Max` — every mapped `string` property must have an explicit
  `MaxLength` and a non-`(max)` column type.
- `DesignTimeServiceProvider_CanActivateEventPublisher` — `dotnet ef`'s design-time service provider
  must be able to resolve `IEventPublisher`/`IMessageBus`; guards a real regression where
  `dotnet ef database update` died mid-seed because the design-time provider had no bus registered.

## Step-by-step

1. Create `Minimal.Infra/Features/<Feature>/Mappers/<Entity>Configs.cs`: `internal sealed class
   <Entity>Configs : DefaultEntityTypeConfiguration<Entity>`, call `base.Configure(builder)` first,
   then indexes, `HasMaxLength`/`HasPrecision` on every column, `HasConversion<string>()` on every
   enum, `ToTable("<Plural>", "<schema>")`.
2. If the feature needs reference data, create `Minimal.Infra/Features/<Feature>/StaticData/<Entity>StaticData.cs`:
   `internal sealed class <Entity>StaticData : DataSeedingConfiguration<Entity>`, override
   `GetDataAsync`, build rows via the entity's `internal` rehydration constructor with fixed `Guid`s
   and `SharedConsts.SystemAccount`.
3. If the feature needs a new domain service, add the interface under `Minimal.Domains/Services/`,
   an `internal sealed` implementation under `Minimal.Infra/Services/`, and one `AddScoped<...>()`
   line in `InfraSetup.AddInfraServices`.
4. Generate and inspect the migration from `ApiEndpoints/`:
   `dotnet ef migrations add <Name> -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj`.
5. `dotnet build -c Release` and `dotnet test --settings coverage.runsettings` from the solution
   root.

## Validation checklist

- [ ] Mapper inherits `DefaultEntityTypeConfiguration<TEntity>` and calls `base.Configure(builder)` first
- [ ] Mapper class is `internal sealed`, under `Minimal.Infra/Features/<Feature>/Mappers/`
- [ ] Every mapped `string` has an explicit `HasMaxLength`; every mapped enum has `HasConversion<string>()`
- [ ] A real business uniqueness needs `.IsUnique()` on the index, not just a validator check
- [ ] `ToTable("Name", "schema")` set — literal string or a `DomainSchemas` constant
- [ ] If seeding: class is `internal sealed`, extends `DataSeedingConfiguration<T>` (not an
      interface), uses the entity's rehydration constructor, and `UseAutoDataSeeding(...)` is
      present in **both** `InfraSetup.AddInfraServices` and `InfraMigration.MigrateDb`
- [ ] If adding a domain service: interface in `Minimal.Domains/Services/`, `internal sealed`
      implementation in `Minimal.Infra/Services/`, explicit `AddScoped<...>()` line added — no
      convention scan will pick it up on its own
- [ ] Migration generated from `ApiEndpoints/` and inspected before continuing
- [ ] `dotnet build -c Release` passes

## Common mistakes

| Mistake | Fix |
|---|---|
| Expecting a namespace like `.Services`/`.Repos` to auto-register an infra service | There's no Scrutor scan for this in the template — add the explicit `AddScoped<TInterface, TImplementation>()` line in `InfraSetup.AddInfraServices` yourself. |
| Wiring `UseAutoDataSeeding`/`UseAutoConfigModel` into only `InfraSetup.AddInfraServices` | Also wire it into `InfraMigration.MigrateDb` — that path builds its own `CoreDbContext`; seed data silently never appears over HTTP otherwise. Real bug this template already hit once. |
| Expecting re-running seeding to update a changed fixed row | `DataSeedingConfiguration<T>` only inserts rows missing by primary key; it never updates an existing row. Change the fixed data via a migration, not by editing `GetDataAsync` and re-running. |
| Seeding via the entity's public constructor | Use the `internal` rehydration constructor — the public one re-raises the entity's created event, which a seed insert should never do. |
| Assuming `[SensitiveData]`/`[Range]`/etc. need mapper configuration | They don't — those are entity/DTO-level concerns (`dknet-domain-entity`, `dknet-appservices-actions`). The mapper only configures storage shape. |
| Forgetting `.Property(p => p.OwnedBy).HasMaxLength(...).IsRequired()` on an `IOwnedBy` entity | Implementing the interface doesn't size or require the column — `ProductConfigs` configures it explicitly. |
| Editing an already-applied migration file by hand | Add a new migration instead; the applied one is a record of what ran against real databases. |
