---
name: dknet-package-adoption
description: Add DKNet's Core, EF Core, messaging/CQRS, or blob-storage NuGet packages to an EXISTING .NET project that was NOT created from the DKNet.Minimal.Template. Use when a consumer wants to reuse pieces of the DKNet framework in their own project layout without scaffolding a new solution.
---

# Skill: Adopting DKNet Packages in an Existing Project

This skill is for a project that already exists with its own namespaces and folder layout — it does not assume `dotnet new dknet-minimal` was run, and never references `Minimal.*` types. If you're scaffolding a brand-new solution from the template instead, use **dknet-project-structure** and the other `dknet-*` skills.

Each package below is independent — install only what the feature needs. All packages target **.NET 10.0+** (EF Core packages additionally need **EF Core 10.0+**); consult `src/Directory.Packages.props` (or your project's own central version file) before adding a version attribute per-project.

---

## When to Use

- Adding one or more `DKNet.*` NuGet packages to a pre-existing .NET API/service/library
- The consuming project has its own entities, `DbContext`, and DI composition root — this skill wires DKNet into that, it doesn't replace it
- NOT for scaffolding a new solution from `DKNet.Minimal.Template` (see `dknet-project-structure`)

## Inputs Required

1. Which capability you need (persistence helpers, repositories, dynamic query filtering, CQRS/messaging, blob storage)
2. Your existing `DbContext` type (if adding EF Core packages) and EF Core provider (SQL Server, PostgreSQL, SQLite, etc. — every package below is provider-agnostic)
3. For blob storage: which backend (Azure Blob Storage, AWS S3, or local filesystem)

---

## Core — `DKNet.Fw.Extensions`

Framework-level extension methods with no dependency on EF Core or any other DKNet package: string/number parsing (`ExtractDigits`, `IsNumber`), `DateTime` helpers (`LastDayOfMonth`, `Quarter`), enum `[Display]` attribute lookup (`GetAttribute<T>`, `GetEnumInfo`), reflection-based property/type checks, and `IAsyncEnumerable` → `List` conversion. Stateless and thread-safe; no DI registration needed.

```bash
dotnet add package DKNet.Fw.Extensions
```

```csharp
using DKNet.Fw.Extensions;

var digits = "Invoice #: INV-2024-00123".ExtractDigits();   // "202400123"
var quarter = DateTime.UtcNow.Quarter();                     // 1-4
```

---

## EF Core Layer

Adopt these incrementally — `Abstractions` alone is useful; `Extensions`, `Repos`, and `Specifications` build on it.

### `DKNet.EfCore.Abstractions`

Base entity classes and interfaces for DDD-flavored persistence: `Entity<TKey>` / `AuditEntity<TKey>` (with domain-event support via `AddEvent(...)`), `ISoftDeletableEntity`, `IConcurrencyEntity`, plus `[Sequence]` / `[SqlSequence]` / `[StaticData]` / `[IgnoreEntity]` attributes.

```bash
dotnet add package DKNet.EfCore.Abstractions
```

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Invoice : AuditEntity<Guid>
{
    public Invoice(string number, string createdBy) : base(createdBy)
    {
        Number = number;
        AddEvent(new InvoiceCreatedEvent(Id, number));
    }

    public string Number { get; private set; }
}

public record InvoiceCreatedEvent(Guid InvoiceId, string Number);
```

### `DKNet.EfCore.Extensions`

Automatic entity configuration discovery, global query filters, and structured data seeding — layered on top of your existing `DbContext`, no `DbSet` rewrite required.

```bash
dotnet add package DKNet.EfCore.Extensions
```

```csharp
using DKNet.EfCore.Extensions;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Existing DbSets stay as they are.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Scans the given assemblies for IEntityTypeConfiguration<T> / IDataSeedingConfiguration<T>
        // implementations instead of requiring them wired up by hand.
        modelBuilder.UseAutoConfigModel<AppDbContext>(config =>
            config.AddAssembly(typeof(Invoice).Assembly));

        base.OnModelCreating(modelBuilder);
    }
}

// Program.cs — provider is whatever this project already uses (SQL Server, PostgreSQL, ...)
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString) // or UseSqlServer / UseSqlite / etc.
           .UseAutoConfigModel<AppDbContext>());
```

Optional: inherit `DefaultEntityTypeConfiguration<TEntity>` for new `IEntityTypeConfiguration<T>` classes to get audit/soft-delete columns configured for free — `base.Configure(builder)` first, then add your own indexes/constraints.

### `DKNet.EfCore.Repos` + `DKNet.EfCore.Repos.Abstractions`

CQRS-flavored repository interfaces (`IRepository<T>`, `IReadRepository<T>`, `IWriteRepository<T>`) plus ready-to-use implementations, with Mapster-projected DTO reads (`GetDto<TModel>`) and transaction helpers — install both together.

```bash
dotnet add package DKNet.EfCore.Repos.Abstractions
dotnet add package DKNet.EfCore.Repos
```

```csharp
// Program.cs
services.AddMapster();
services.AddGenericRepositories<AppDbContext>();

// Usage
public class InvoiceService(IRepository<Invoice> repository)
{
    public async Task<Invoice> CreateAsync(string number, CancellationToken ct)
    {
        var invoice = new Invoice(number, "system");
        await repository.AddAsync(invoice, ct);
        await repository.SaveChangesAsync(ct);
        return invoice;
    }

    public Task<List<InvoiceDto>> GetOpenInvoicesAsync(CancellationToken ct) =>
        repository.GetDto<InvoiceDto>(i => !i.IsClosed).ToListAsync(ct);
}

public record InvoiceDto(Guid Id, string Number);
```

Need a custom repository with extra query methods? Extend the concrete `Repository<TEntity>` class rather than reimplementing the interfaces from scratch.

### `DKNet.EfCore.Specifications`

Composable query objects (`Specification<TEntity>`) plus runtime dynamic-predicate building (`DynamicAnd`/`DynamicOr` over `(propertyName, operation, value)` triples) — useful for search/filter endpoints where the filter shape isn't known at compile time. Requires `DKNet.EfCore.Repos.Abstractions` for the `IRepositorySpec` extension methods it hangs off.

```bash
dotnet add package DKNet.EfCore.Specifications
```

```csharp
public class InvoiceSearchSpec : Specification<Invoice>
{
    public InvoiceSearchSpec(string? numberContains, bool? isClosed)
    {
        var predicate = PredicateBuilder.New<Invoice>(true);

        if (!string.IsNullOrEmpty(numberContains))
            predicate = predicate.DynamicAnd("Number", DynamicOperations.Contains, numberContains);
        if (isClosed.HasValue)
            predicate = predicate.DynamicAnd("IsClosed", DynamicOperations.Equal, isClosed.Value);

        WithFilter(predicate);
    }
}

// var results = await repository.ToListAsync(new InvoiceSearchSpec(numberContains: "INV-2024", isClosed: false));
```

`.AsExpandable()` is required if you build predicates directly against `DbContext` instead of going through the repository extensions (which apply it for you).

---

## Messaging / CQRS — `DKNet.SlimBus.Extensions`

Fluent request/query/event-handler interfaces over SlimMessageBus with automatic EF Core `SaveChanges` after a successful command and `FluentResults`-based error handling — no MediatR-style pipeline needed.

```bash
dotnet add package DKNet.SlimBus.Extensions
```

```csharp
// Program.cs
services.AddSlimBusForEfCore(builder => builder
    .WithProviderMemory() // in-process; swap for a real transport when you need cross-process messaging
    .AutoDeclareFrom(typeof(CreateInvoiceHandler).Assembly)
    .AddJsonSerializer());

// Command
public record CreateInvoice(string Number) : Fluents.Requests.IWitResponse<InvoiceDto>;

internal sealed class CreateInvoiceHandler(AppDbContext db, IMapper mapper)
    : Fluents.Requests.IHandler<CreateInvoice, InvoiceDto>
{
    public async Task<IResult<InvoiceDto>> OnHandle(CreateInvoice request, CancellationToken ct)
    {
        if (await db.Set<Invoice>().AnyAsync(i => i.Number == request.Number, ct))
            return Result.Fail<InvoiceDto>($"Invoice {request.Number} already exists.");

        var invoice = new Invoice(request.Number, "system");
        db.Add(invoice);
        // SaveChanges runs automatically after a successful handler — no explicit call needed.
        return Result.Ok(mapper.Map<InvoiceDto>(invoice));
    }
}
```

Commands auto-save on success; queries (`Fluents.Queries.IHandler<...>`) never trigger a save, since they're read-only.

---

## Blob Storage — `DKNet.Svc.BlobStorage.Abstractions` + a provider

`IBlobService` is the provider-agnostic contract (`SaveAsync`, `GetAsync`, `ListItemsAsync`, `DeleteAsync`, `ExistsAsync`, plus `SaveTextAsync`/`SaveJsonAsync` extensions); pick exactly one provider package for the backend you actually run against.

```bash
dotnet add package DKNet.Svc.BlobStorage.Abstractions
dotnet add package DKNet.Svc.BlobStorage.AzureStorage   # or .AwsS3 / .Local
```

```json
// appsettings.json
{
  "BlobService": {
    "AzureStorage": {
      "ConnectionString": "UseDevelopmentStorage=true",
      "ContainerName": "documents"
    }
  }
}
```

```csharp
// Program.cs
services.AddAzureStorageAdapter(configuration);   // registers IBlobService

// Usage — identical code regardless of which provider package is installed
public class DocumentService(IBlobService blobService)
{
    public Task<string> UploadAsync(string fileName, Stream content, string contentType) =>
        blobService.SaveAsync(new BlobData
        {
            Name = $"documents/{fileName}",
            ContentStream = content,
            ContentType = contentType,
        });
}
```

Swapping providers later (e.g. `.Local` in dev, `.AzureStorage` in production) only changes the registration call and configuration section — application code against `IBlobService` doesn't change.

---

## Validation Checklist

- [ ] Only the packages the feature actually needs were added (no blanket "add everything")
- [ ] EF Core additions layer onto the existing `DbContext`/provider — no assumption of a specific database engine
- [ ] No `Minimal.*` namespace or template folder path (`Minimal.Domains`, `Minimal.AppServices`, …) appears anywhere in the guidance followed
- [ ] Repositories/specs/handlers registered in DI (`AddGenericRepositories`, `AddSlimBusForEfCore`, `AddAzureStorageAdapter`, etc.) — nothing relies on auto-discovery unless the package documents it
- [ ] For blob storage, exactly one provider package installed alongside `Abstractions`
- [ ] `dotnet build` passes with the new package references

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Adding `DKNet.EfCore.Repos` without `.Repos.Abstractions` | Both are required — the interfaces and the implementations ship separately |
| Building a dynamic predicate directly against `DbContext` without `.AsExpandable()` | Required for LinqKit to translate the expression; the `IRepositorySpec` extensions already apply it |
| Installing more than one blob storage provider package for the same `IBlobService` | Register exactly one — the last registration wins and the others are dead weight |
| Assuming a specific EF Core provider (SQL Server, Postgres, …) is required | Every package here is provider-agnostic; it only needs a working `DbContext` |
| Copying `Minimal.*` namespaces/paths from the template's docs | This skill — and any project using it — has its own namespaces; the template's layout doesn't apply |

## Next Steps

Once the package is wired in, follow that package's own README under the `DKNet` repo checkout (`src/Core/DKNet.Fw.Extensions`, `src/EfCore/DKNet.EfCore.*`, `src/SlimBus/DKNet.SlimBus.Extensions`, `src/Services/DKNet.Svc.BlobStorage.*`) for deeper API reference and advanced scenarios (custom query filters, keyset pagination, interceptors, retry policies).
