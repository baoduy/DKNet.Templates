# Copilot Project Instructions

- 🧠 Read `/memory-bank/memory-bank-instructions.md` first.
- 🗂 Load all `/memory-bank/*.md` before any task.
- 🚦 Use the Kiro-Lite workflow: PRD → Design → Tasks → Code.
- 🔒 Follow security & style rules in `copilot-rules.md`.
- 📝 On "/update memory bank", refresh activeContext.md & progress.md.

These guidelines help generate consistent, safe, high‑quality code for the Monxa Payment Gateway (.NET 9, modular Clean-ish architecture).

## Solution Architecture (High Level)
Projects (prefix `Mx.Pgw.*`):
- Api: Thin startup + endpoint wiring. No business logic.
- AppServices: Application layer (Features/** folders: Actions, Queries, Services, Specs, EventHandlers). Orchestrates domain + infra.
- Domains: Core domain models, aggregates, events, value objects, enums, specs (if truly domain-specific).
- Infra: EF Core DbContexts, Migrations, Repositories, persistence, external impls.
- AppHost / AppOnlyHost: Composition roots / hosting variants (jobs, workers, app-only runtime scenarios).
- LogInfra / Share: Cross-cutting concerns, shared constants, options, primitives, converters.
- Clients (`Durian.*`, `LaunCX.*`, `Mx.Identity.Clients`, `OpenExchange.Clients`): External service integrations.
- Tests: `*.UnitTests`, `*.IntegrateTests`, `AppServices.Tests`.

Goal: Keep boundaries clear; avoid leaking EF types or external SDKs into Domain or outward through public DTOs.

## Core Patterns
1. CQRS-ish Feature Organization
   - Each logical feature has a folder under `AppServices/Features/<FeatureName>/` with subfolders: `Actions/` (commands), `Queries/`, `Services/`, `EventHandlers/`, `Specs/`.
   - Use record types for immutable request/response shapes.
   - Command = state change (inherits `BaseCommand` if auditing needed).
   - Query = read model (inherits `PageableQuery` etc when paging).
2. Validation
   - Use FluentValidation. Place validators in same file (internal sealed class). Naming: `<TypeName>Validator`.
   - Fail fast; include `.When(...)` for conditional rules.
3. Handlers
   - Internal sealed class named `<RequestTypeName>Handler` implementing appropriate Fluents interface: e.g. `Fluents.Requests.IHandler<TRequest, TResponse>` or `Fluents.Queries.IPageHandler<TQuery, TDto>`.
   - Method: `OnHandle(TRequest request, CancellationToken cancellationToken)`.
   - Return types: `Task<IResult<T>>` for commands or `Task<IPagedList<TDto>>` for paged queries.
4. Mapping
   - Use Mapster (`TypeAdapterConfig`). Add new mappings via scoped partial mapping files if needed; prefer attribute-based or scan assembly.
5. Repositories & Specs
   - Use interfaces: `IRepository<T>`, `IReadRepository<T>`, `IRepositoryFactory`.
   - Specification classes start with `Spec` prefix, are immutable, and encapsulate filtering logic. Keep infra-specific bits (includes, EF expressions) inside specs.
6. Events
   - Domain events added via aggregate methods (`AddEvent<...>()`). Event handlers live under `EventHandlers/` folder.
7. Enums & Constants
   - UpperCamelCase for enum members (e.g., `ChannelCodes.QrQris`). Provide an `Unknown` or `None` sentinel where appropriate; validators must exclude them.
8. Error Handling
   - Use `Result` pattern (e.g., `FluentResults` style) for command failures. Avoid throwing for validation & business rule errors.
9. Async
   - All I/O methods async; never block (`.Result` / `.Wait()`). ConfigureAwait not required unless library code.
10. Logging & Telemetry
    - Rely on DI-provided logger abstractions; do not instantiate loggers directly.

## Naming & File Organization
- One public request/record per file unless small + variant (e.g., base + derived request).
- Validators & handlers can share the request file if short; otherwise split when > ~200 lines.
- DTOs end with `Dto` or `ActionsDto` (when representing action outcomes with follow-up steps/links).
- Factory interfaces: `IFeatureThingFactory` -> implement `FeatureThingFactory`.

## Adding a New Command Example
1. Create record under `Features/<Feature>/Actions/`: `public sealed record ConfirmSomethingRequest : BaseCommand, IWitResponse<SomethingDto> { ... }`.
2. Add `ConfirmSomethingValidator` with rules.
3. Implement `ConfirmSomethingHandler` injecting only needed abstractions (repositories, mapper, provider interfaces).
4. Map domain -> DTO using Mapster or manual mapping if complex.
5. Add unit test(s) under `AppServices.Tests/Features/<Feature>/Actions/` verifying validation + success path.
6. Expose via endpoint (in Api project) using minimal API or endpoint config pattern (see existing `UseEndpointConfigs`). Keep endpoint file small and feature-specific.

## Query Guidance
- Query records extend `PageableQuery` when pagination needed, implement interface specifying response element type.
- Validator enforces `enum` validity, string length, optional filters.
- Handler gets repository, applies `repository.QuerySpecs(new SpecWhatever(...))`, then `.ProjectToType<...>(mapper.Config)` and `.ToPagedListAsync(page, size)`.

## EF Core / Migrations
- Migrations live in `Infra/Migrations`. Use provided scripts:
  - `./add-migration.sh <Name>`: adds migration.
  - `./remove-migration.sh`: removes last.
- Never hand-edit designer migration code except for safe seed/data adjustments.
- Schemas: `pgw` (payment), `static` (static data) — use constants `InfraConsts.PaymentSchema`, etc.
- If adding new entity:
  1. Define domain model in Domains (avoid EF attributes; use Fluent config in Infra if needed).
  2. Add EF configuration (if custom) under Infra/Contexts/Configurations.
  3. Add repository/spec adjustments.
  4. Add migration script via shell helper.

## Dependency Injection
- Register new services in `AppSetup.AddAppServices` or feature-specific extension methods under `Extensions/`.
- Prefer interface per service; scope choices:
  - Database-related repositories: scoped.
  - Mappers: singleton for config; mapper service can be scoped.
  - External API clients: typed HTTP clients with resiliency policies (consider Polly if needed in future).

## Validation Rules (Consistency Checklist)
- For optional strings: `.When(x => !string.IsNullOrEmpty(x.Prop))` + `.Must(...).MaximumLength(n)`.
- For enums: `.IsInEnum()` AND `.NotEqual(EnumType.Unknown)` when sentinel exists.
- For dictionary metadata: enforce key/value length constraints.

## DTO & Serialization Conventions
- Use `JsonPropertyName` where external contract differs from internal naming.
- Keep enums serialized as their string names (configure globally if needed) — if custom casing required, create converter under Share.
- Monetary values: `decimal` (never double). Formatting responsibilities delegated to currency services (e.g., `ICurrencyRepository.Format`).

## Testing Guidance
- Unit tests: single assertion principle when practical; group by context (Arrange/Act/Assert). Name pattern: `MethodName_StateUnderTest_ExpectedOutcome`.
- Feature tests for handlers validate:
  1. Validation failure for bad input.
  2. Success path returns expected DTO fields.
  3. Domain events added where expected.
- Integration tests: exercise persistence + spec queries + migrations.

## Performance & Safety
- Avoid N+1 by projecting queries before enumeration.
- Use pagination for any collection > potential 100 items.
- Keep handlers ≤ ~80 lines; extract pure logic into private methods or services.

## Adding External Integrations
- Create a `*Options` class (bound from configuration) & setup extension method `Add<ProviderName>Client` similar to existing clients.
- Authentication handlers inherit pattern shown in clients (e.g., `DurianAuthHandler`).

## Jobs / Background Operations
- Job mode triggered by command-line args (see `Program.cs`: `args.TryGetJobType()`). When writing a new job:
  - Implement job logic in AppServices/Jobs or a Feature-specific job folder.
  - Add enumeration value to `JobTypes` (if defined) and wiring in host builder extension (`RunJobAsync`).

## Logging & Observability
- Use structured logging: `_logger.LogInformation("Charge created {ChargeId}", charge.Id);`
- Add relevant OpenTelemetry instrumentation only through config (no manual Activity creation unless spanning multi-step processes not auto-instrumented).

## Security & Compliance
- No secrets in source. Use configuration + environment variables + Azure App Configuration.
- Validate all externally provided identifiers (GUID existence, etc.).
- Normalize/uppercase/lowercase codes early (use custom converters like `UpperCaseConvertor`).

## Prompt Patterns for Copilot
Provide explicit directions with context and constraints:
- "Generate a new command + validator + handler in `Features/Orders/Actions` to cancel an order (ensuring status transitions from Pending only). Return an OrderActionsDto with updated status and cancellation timestamp. Follow existing Create charge patterns."
- "Add a paged query under `Features/Transactions/Queries` to list transactions filtered by MerchantId and optional date range; validate that end date >= start date; return a list of TransactionDto."
- "Create migration to add TwoFactorEnabled column to Merchant (bit, default false) under `pgw` schema; update domain model, EF config, and validator if needed."

ALWAYS reference existing similar feature before generating code.

## Disallowed / Avoid
- Adding business logic directly in API endpoints or Program.cs.
- Exposing EF entities or DbContext in other layers.
- Using magic strings for schema names — use constants.
- Synchronous I/O or blocking calls.
- Large multi-purpose handlers or validators (> ~200 lines).

## Definition of Done Checklist (Automate mentally per change)
- [ ] Follows folder & naming conventions.
- [ ] Validator covers all input invariants.
- [ ] Handler uses repositories/specs only; no direct DbContext.
- [ ] Mapping works (compile-time Mapster validation passes if enabled).
- [ ] Tests added/updated (unit + integration if persistence affected).
- [ ] Migration created (if persistence model changed) & builds.
- [ ] No analyzers warnings introduced (consider Meziantou.Analyzer recommendations).
- [ ] No secrets/config hardcoded.

## Lightweight Examples
Command skeleton:
```csharp
public sealed record DoSomethingRequest : BaseCommand, Fluents.Requests.IWitResponse<SomethingDto>
{
    public Guid MerchantId { get; init; }
    public decimal Amount { get; init; }
}

internal sealed class DoSomethingRequestValidator : AbstractValidator<DoSomethingRequest>
{
    public DoSomethingRequestValidator(IReadRepository<Merchant> repo)
    {
        RuleFor(x => x.MerchantId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
    }
}

internal sealed class DoSomethingRequestHandler(
    IRepository<Something> somethingRepo,
    IReadRepository<Merchant> merchantRepo,
    IMapper mapper)
    : Fluents.Requests.IHandler<DoSomethingRequest, SomethingDto>
{
    public async Task<IResult<SomethingDto>> OnHandle(DoSomethingRequest request, CancellationToken ct)
    {
        var merchant = await merchantRepo.SpecsFirstAsync(new SpecGetActiveMerchantOnlyById(request.MerchantId), ct);
        var entity = Something.Create(merchant, request.Amount, request.ByUser);
        await somethingRepo.AddAsync(entity, ct);
        await somethingRepo.SaveChangesAsync(ct);
        return mapper.ResultOf<SomethingDto>(entity);
    }
}
```

Paged query skeleton:
```csharp
public sealed record ListThingsQuery : PageableQuery, Fluents.Queries.IWitResponse<ThingDto>
{
    public Guid MerchantId { get; init; }
}

internal sealed class ListThingsQueryValidator : AbstractValidator<ListThingsQuery>
{
    public ListThingsQueryValidator() => RuleFor(x => x.MerchantId).NotEmpty();
}

internal sealed class ListThingsQueryHandler(IReadRepository<Thing> repo, IMapper mapper)
    : Fluents.Queries.IPageHandler<ListThingsQuery, ThingDto>
{
    public Task<IPagedList<ThingDto>> OnHandle(ListThingsQuery request, CancellationToken ct) =>
        repo.QuerySpecs(new SpecListThings(request.MerchantId))
            .ProjectToType<ThingDto>(mapper.Config)
            .ToPagedListAsync(request.PageNumberValue, request.PageSizeValue);
}
```

## When Unsure
Search for an existing closest pattern within the same Feature or a parallel Feature (e.g., Charges/Create) and replicate style with minimal divergence.

---
End of Copilot Instructions.

