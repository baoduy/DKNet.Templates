---
name: arckit-design-skill
description: "Design implementation-ready architecture for new features before coding. Produces class-first OOP designs, clear component boundaries, DI strategies, and actionable decisions rooted in spec and plan requirements."
metadata:
  category: architecture
  type: design
  complexity: high
  estimated-time: "1-2 hours per feature"
---

# arckit-design-skill

## Purpose

Design implementation-ready architecture for a new feature before any coding starts. This skill produces class-first OOP designs, clear component boundaries, dependency injection strategies, and actionable architecture decisions rooted in spec and plan requirements.

## When to Use

- You're designing architecture for a **new feature** (not analyzing existing code).
- You have a spec.md and plan.md that define requirements and design constraints.
- You need to decide on layer boundaries, component roles, async boundaries, error handling, and testing strategies.
- You're creating architecture.md and architecture-review.md as pre-implementation handoff.

## When NOT to Use

- **Analysis mode**: You're analyzing how an existing feature works (use arckit-analysis-skill instead).
- **Q&A mode**: You're asking questions about existing architecture (use arckit-qa-skill instead).
- **No spec/plan**: If you lack a detailed spec.md and plan.md, clarify requirements first (suggest speckit.specify or speckit.clarify).

## Scope

- **One feature at a time** under `specs/<feature>/`.
- **Full design coverage**: goals, drivers, constraints, layered design, components, data/persistence, API contracts, async/reliability, security, observability, testing, deployment, risks, readiness.
- **Class-first OOP**: propose concrete classes, interfaces, and dependency injection structure.
- **Repository conventions**: mirror patterns from Profiles/V1, respect Api → AppServices → Domains/Infra boundaries.
- **No implementation code**: keep design actionable without writing actual handlers/services.

## Process

### 1. Load Required Inputs
Before starting, read:
- [ ] `specs/<feature>/spec.md` (requirements, scope, business objectives)
- [ ] `specs/<feature>/plan.md` (design approach, high-level structure, decisions)
- [ ] `specs/<feature>/research.md` (if present: domain knowledge, patterns)
- [ ] `specs/<feature>/data-model.md` (if present: entity definitions)
- [ ] `specs/<feature>/contracts/` (if present: API, events, external schemas)
- [ ] Repository rules in `AGENTS.md` and `copilot-instructions.md`

If any critical document is missing, ask the user before proceeding. Do not invent architecture from thin air.

### 2. Detect Architecture Gaps
Analyze the plan.md for completeness:
- **Gaps found?** "We haven't decided how status transitions are modeled"
- **Unresolved conflicts?** "Spec says async, but plan shows sync handler"
- **Unclear boundaries?** "Is payment reconciliation part of this feature or separate?"

**If gaps exist**, ask up to 5 targeted clarification questions and wait for answers before finalizing the design.

### 3. Map Feature to Repository Patterns
- **Endpoint location**: Api/ApiEndpoints/<Feature>V1/...
- **Command/Query handlers**: AppServices/Features/<Feature>/Actions/ or /Queries/
- **Validators**: same file as command, internal sealed class
- **Services**: AppServices/Features/<Feature>/Services/
- **Domain aggregate**: Mx.Pgw.Domains/.../<DomainName>/ (UpperCamelCase)
- **Event handlers**: AppServices/Features/<Feature>/EventHandlers/
- **Repositories**: Infra layer, use generic IRepository<T> and specs
- **EF Core config**: Infra/Contexts/Configurations/<Entity>Configuration.cs
- **Migrations**: Infra/Migrations/ (use add-migration.sh script)

### 4. Design Layered Architecture
Respect the standard .NET vertical-slice pattern:

**API Layer** (thin, no business logic):
- Endpoint wiring → handler call → response.
- OpenAPI documentation.
- Request/response mapping.

**Application Layer** (orchestration):
- Commands/Queries → validation → handler → domain call → repo write → event → response.
- Mapster configuration for DTO mapping.
- Feature-scoped services and factories.

**Domain Layer** (core business rules):
- Aggregate roots (entities with invariants).
- Value objects (immutable, no identity).
- Domain events (state changes).
- Specifications (filtering/query logic where truly domain-specific).

**Infra Layer** (persistence and external integrations):
- EF Core DbContext, configurations, migrations.
- Generic repositories implementing IRepository<T>, IReadRepository<T>.
- External client implementations.
- Event publishers/subscribers wiring.

### 5. Design Components with Class-First OOP
For each major responsibility, propose:
- **Interface**: public contract.
- **Implementation**: concrete class, internal sealed.
- **Injection point**: where does it get injected (handler, service, endpoint)?
- **Lifetime**: singleton, scoped, transient.
- **Testability**: what hooks exist for mocking/testing?

Example slot to fill:

| Component | Interface | Implementation | Injected Into | Lifetime | DI Setup |
|---|---|---|---|---|---|
| Feature service | IOrderSummaryService | OrderSummaryService | CreateOrderHandler | Scoped | AddOrderServices (ext method) |
| Mapper | IMapper | Mapster config | Handler | Singleton | TypeAdapterConfig.GlobalSettings |
| Repo | IRepository<Order> | Repository<Order> | Service | Scoped | AddRepositories |

### 6. Design Data and Persistence Architecture
Address:
- **Entity hierarchy**: aggregates, child entities, value objects.
- **EF Core patterns**: owned types, table-per-type, shared tables?
- **Consistency model**: single transaction, eventual consistency, saga?
- **Key generation**: identity, GUID, natural keys?
- **Projection queries**: if read models needed, how are they computed?
- **Migration strategy**: incremental, blue-green, feature flags?

Propose EF configuration class names and navigation property patterns.

### 7. Design API and Contract Architecture
Define:
- **Request DTOs**: immutable records, validation rules.
- **Response DTOs**: naming (Dto, ActionsDto), serialization contracts.
- **Error format**: error codes, messages, details.
- **Pagination**: if applicable, use `DKNet.AspCore.Extensions`' `MapGetList` (pageNumber/pageSize, defaults 1/20, ceiling 100).
- **Versioning**: v1, v2 routes or backwards-compatible?
- **OpenAPI**: what tags, summaries, descriptions?

Propose DTO class names and validation rule matrix.

### 8. Design Async, Concurrency, and Reliability
Address:
- **Async boundaries**: where do I/O operations occur (repos, clients)?
- **Concurrency**: can two requests race? Are there optimistic locks or version numbers?
- **Transactionality**: when does SaveChangesAsync happen? Compensating transactions needed?
- **Timeouts**: what are timeout values for external calls, handler execution?
- **Retry logic**: exponential backoff, circuit breakers (Polly)?
- **Idempotency**: can this operation be safely retried?

Propose timeout values, retry strategies, and idempotency keys if needed.

### 9. Design Security and Compliance Strategy
Address:
- **Access control**: who can invoke this feature? Claims, roles, policies?
- **Data sensitivity**: PII, financial data, regulated data?
- **Encryption**: TLS for transit, encryption at rest?
- **Audit trail**: what actions are logged, what details captured?
- **Masking**: which fields should be masked in logs?
- **Regulatory**: GDPR retention, PCI-DSS, SOX, HIPAA?

Propose validation rules, audit event fields, encryption/hashing decisions.

### 10. Design Observability and Operational Readiness
Address:
- **Logging**: structured logging, key events, context (user, tenant, request ID).
- **Metrics**: latency buckets, throughput, error rates, business metrics.
- **Tracing**: which cross-service calls should be traced?
- **Health checks**: liveness, readiness, dependency health.
- **Dashboards**: what operational dashboards should exist?
- **Alerts**: what error thresholds and SLOs?

Propose log statement locations, metric names, alert rules.

### 11. Design Testing Strategy and Quality Gates
Address:
- **Unit tests**: handlers, validators, services, mapping.
- **Integration tests**: persistence, external service mocks, event handling.
- **End-to-end tests**: full request flow, user journeys.
- **Edge cases**: boundary values, error conditions, concurrency.
- **Test fixtures**: shared test data, builders, factories.
- **Coverage target**: 80%+ line coverage, 100% critical path.

Propose test class locations and test scenarios by layer.

### 12. Design Deployment and Runtime Considerations
Address:
- **Configuration**: environment variables, secrets, feature flags?
- **Migrations**: rollforward, rollback, zero-downtime?
- **Scaling**: horizontal scaling, limits, quotas?
- **Dependencies**: external services required, service discovery, health checks?
- **Backward compatibility**: can old clients call this API after deploy?

Propose configuration classes, migration strategy, feature flags.

### 13. Identify Risks, Trade-offs, and Open Questions
- **Reliability risks**: single points of failure, cascading failures, timeouts?
- **Performance risks**: N+1 queries, synchronous calls, locking?
- **Security risks**: injection, privilege escalation, sensitive data logging?
- **Compliance risks**: data retention, deletion, audit trail completeness?
- **Maintenance risks**: code complexity, test coverage, documentation?
- **Trade-offs**: simplified design vs. robustness, strong vs. eventual consistency?
- **Open questions**: unclear requirements, architectural unknowns?

Provide a ranked list (Impact × Effort matrix).

### 14. Create Implementation Readiness Checklist
Verify the design is actionable:
- [ ] All components have concrete class names and interface contracts.
- [ ] All async boundaries clearly marked.
- [ ] All error handling paths defined.
- [ ] All external dependencies identified.
- [ ] All data migrations planned.
- [ ] All security checks proposed.
- [ ] All test scenarios outlined.
- [ ] All configuration/option classes sketched.
- [ ] All risks ranked and mitigation proposed.
- [ ] All gaps and dependencies documented.

## Output Artifacts

Create or update these files under `specs/<feature>/`:

### architecture.md (14 Sections, ~3000–5000 words)

Follow the architecture.md contract from the unified agent spec:

1. **Feature Scope and Goals**
   - What problem does this feature solve?
   - Success criteria and measurable objectives.

2. **Architectural Drivers and Constraints**
   - Requirements that shape architecture (performance, scale, latency, compliance).
   - Constraints from platform, infrastructure, team, timeline.

3. **System Context and Boundaries**
   - Where does this feature fit in the larger system?
   - External systems it interacts with.
   - Responsibilities this feature owns vs. other features.

4. **Layered Design and Responsibilities**
   - API Layer: endpoint, request/response mapping, OpenAPI.
   - Application Layer: handler, validator, services, factory.
   - Domain Layer: aggregates, events, value objects.
   - Infra Layer: repositories, EF configs, external clients.
   - Cross-cutting: logging, auth, error handling.

5. **Component Design (Class-First OOP)**
   - Major classes/interfaces with constructor injection.
   - Dependency diagram showing service relationships.
   - Naming and file location for each component.
   - Lifetime (scoped, singleton, transient) justification.

6. **Data and Persistence Architecture**
   - Entity hierarchy and EF configuration approach.
   - Consistency model (ACID, eventual, compensating).
   - Query patterns and projection strategy.
   - Migration approach with rollback plan.

7. **API and Contract Architecture**
   - Request/response DTOs with structure examples.
   - Validation rules and error codes.
   - OpenAPI tagging and documentation.
   - Versioning strategy.

8. **Async, Concurrency, and Reliability Strategy**
   - All async boundaries and why.
   - Concurrency handling (optimistic locks, versioning, isolation).
   - Timeout values and retry strategies.
   - Idempotency and deduplication approach.

9. **Security and Compliance Strategy**
   - Access control (claims, roles, policies).
   - Data protection (encryption, masking, retention).
   - Audit trail and compliance requirements.
   - Threat model and mitigations.

10. **Observability and Operational Readiness**
    - Structured logging strategy and key events.
    - Metrics and KPIs to track.
    - Health checks and readiness signals.
    - Alerting rules and runbooks.

11. **Testing Strategy and Quality Gates**
    - Unit, integration, end-to-end test coverage.
    - Test scenarios and fixtures.
    - Coverage targets and tools.

12. **Deployment and Runtime Considerations**
    - Configuration and secrets strategy.
    - Migration execution and rollback.
    - Feature flag approach if applicable.
    - Scaling and limits.

13. **Risks, Trade-offs, and Open Questions**
    - Ranked by impact × effort.
    - Mitigation strategies.
    - Unresolved design questions requiring discussion.

14. **Implementation Readiness Checklist**
    - All 13 items above validated.
    - Dependencies and blockers identified.
    - Go/no-go readiness for task generation.

### architecture-review.md

Validate the architecture against three criteria:

**Required Sections Checklist**
- [ ] All 14 sections of architecture.md filled and complete.
- [ ] Each section has concrete decisions (not placeholders).
- [ ] Component names are consistent with repository conventions.

**.NET Best Practices Checklist**
- [ ] Interfaces and DI boundaries are clear.
- [ ] Constructor injection (not service locator, no static factory patterns).
- [ ] Async-first boundaries for I/O and external calls.
- [ ] Structured logging and explicit error handling.
- [ ] Strongly typed configuration (IOptions<T>).
- [ ] SOLID principles applied (single responsibility, open/closed, etc.).
- [ ] Testability by design (mockable interfaces, separable concerns).

**Repository Architecture Constraints**
- [ ] Respects vertical-slice pattern.
- [ ] Api → AppServices → Domains/Infra layer boundaries maintained.
- [ ] Features organized under AppServices/Features/<Feature>/.
- [ ] Naming follows UpperCamelCase for commands, queries, DTOs.
- [ ] Event handlers under EventHandlers/ subfolder.
- [ ] Repositories use generic IRepository<T> and specs.
- [ ] EF configurations under Infra/Contexts/Configurations/.
- [ ] Migrations use add-migration.sh helper script.

**Pass/Fail Verdict**

| Check | Pass | Fail | Notes |
|---|---|---|---|
| Required sections | 14/14 | ? | |
| Best practices | 7/7 | ? | |
| Repository constraints | 8/8 | ? | |
| **OVERALL** | ✅ **READY** | ❌ **BLOCKED** | List remediations below |

**Remediation List** (if any failures)
- "❌ Missing async boundary definition for external API calls → Add to section 8."
- "❌ Test strategy unclear → Propose test layers and scenarios."

## Quality Checklist

- ✅ **14 sections complete** in architecture.md with concrete decisions, not placeholders.
- ✅ **Architecture review pass**: all required sections, best practices, repository constraints checked.
- ✅ **Component naming**: all classes, interfaces, DTOs follow repository conventions.
- ✅ **Dependency clarity**: every component shows constructor injection, lifetimes, and testability.
- ✅ **Data model defined**: entity hierarchy, EF configuration approach, consistency model.
- ✅ **Async boundaries explicit**: every I/O method marked async, timeout/retry values specified.
- ✅ **Security and compliance**: access control, data protection, audit trail addressed.
- ✅ **Testing outlined**: unit, integration, end-to-end scenarios with coverage target.
- ✅ **Risks identified and ranked**: impact × effort matrix with mitigations.
- ✅ **Readiness checklist**: go/no-go signal for task generation.

## Hints and Patterns

### Command Slot
```csharp
public sealed record CreateThingRequest : BaseCommand, IWitResponse<ThingDto>
{
    public required string Name { get; init; }
    public required decimal Amount { get; init; }
}

internal sealed class CreateThingRequestValidator : AbstractValidator<CreateThingRequest>
{
    public CreateThingRequestValidator(IReadRepository<Tenant> tenantRepo)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Amount).GreaterThan(0m);
    }
}

internal sealed class CreateThingRequestHandler(
    IRepository<Thing> repo,
    IMapper mapper)
    : Fluents.Requests.IHandler<CreateThingRequest, ThingDto>
{
    public async Task<IResult<ThingDto>> OnHandle(CreateThingRequest request, CancellationToken ct)
    {
        var entity = Thing.Create(request.Name, request.Amount, request.ByUser);
        await repo.AddAsync(entity, ct);
        await repo.SaveChangesAsync(ct);
        return mapper.ResultOf<ThingDto>(entity);
    }
}
```

### Query Slot (Paged)
```csharp
public sealed record ListThingsQuery : IWitResponse<ThingDto>
{
    public Guid TenantId { get; init; }
    public string? FilterByName { get; init; }
}

internal sealed class ListThingsQueryValidator : AbstractValidator<ListThingsQuery>
{
    public ListThingsQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.FilterByName).MaximumLength(100).When(x => x.FilterByName != null);
    }
}

internal sealed class ListThingsQueryHandler(IReadRepository<Thing> repo, IMapper mapper)
    : Fluents.Queries.IPageHandler<ListThingsQuery, ThingDto>
{
    public Task<IPagedList<ThingDto>> OnHandle(ListThingsQuery request, CancellationToken ct) =>
        repo.QuerySpecs(new SpecListThings(request.TenantId, request.FilterByName))
            .ProjectToType<ThingDto>(mapper.Config)
            .ToPagedListAsync(request.PageNumberValue, request.PageSizeValue);
}
```

### Service Interface Slot
```csharp
public interface IOrderCancellationService
{
    Task CancelAsync(Guid orderId, string reason, CancellationToken cancellationToken);
    Task<bool> CanCancelAsync(Guid orderId, CancellationToken cancellationToken);
}

internal sealed class OrderCancellationService(
    IRepository<Order> repo,
    IEventPublisher eventPublisher)
    : IOrderCancellationService
{
    public async Task CancelAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var order = await repo.FirstAsync(new SpecGetOrderById(orderId), ct);
        order.Cancel(reason);
        await repo.SaveChangesAsync(ct);
        await eventPublisher.PublishAsync(order.GetEvents(), ct);
    }

    public async Task<bool> CanCancelAsync(Guid orderId, CancellationToken ct)
    {
        var order = await repo.FirstAsync(new SpecGetOrderById(orderId), ct);
        return order.Status == OrderStatus.Pending;
    }
}
```

## Before You Start

Before running design, ensure:
- [ ] You have read and understood specs/<feature>/spec.md
- [ ] You have read and understood specs/<feature>/plan.md
- [ ] Feature scope boundaries are clear (what is/isn't included)
- [ ] You have access to existing features for pattern reference
- [ ] Critical design decisions from plan.md have been identified
- [ ] Any gaps or conflicts in plan.md have been flagged

---

**End of arckit-design-skill**
