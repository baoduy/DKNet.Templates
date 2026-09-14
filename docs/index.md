# DKNet.Minimal.Template Documentation

Reference docs for the `DKNet.Minimal.Template` — a NuGet solution template that scaffolds
production-ready .NET 10 microservices using vertical-slice DDD/CQRS.

## Getting started

- [Template Usage Reference](template-usage.md) — install the template, scaffold a solution, then
  run, test, and publish it.
- [Template Feature List](template-features.md) — everything `dotnet new dknet-minimal` wires up
  before you write feature code.
- [DKNet Package Inventory](dknet-packages.md) — the DKNet NuGet family, one package per capability.
- [Configuration Reference](configuration-reference.md) — every `appsettings` key a generated
  solution reads: meaning, default, effect, and the code path that reads it.
- [Extension Points](extension-points.md) — where your own code attaches, and the boundaries the
  architecture tests hold you to.

## Building a feature

- [DDD Implementation Guide](ddd-implementation-guide.md) — add one vertical-slice feature, entity
  to endpoint.
- [CRUD Attributes](crud-attributes.md) — build a full CRUD slice from four attributes, domain actions included
  (generator-driven).
- [Querying and Specifications](querying-and-specifications.md) — the read side, from HTTP request
  to paged projection.
- [Generic List Endpoint](generic-list-endpoint.md) — the filter/search/order/page contract every
  generated CRUD list route exposes for free.

### Capabilities you can reach for

Everything below already exists in a generated solution — you opt into it per feature, usually with
one attribute or one call. Nothing here needs new infrastructure.

**Write side — shaping the slice**

| Capability | Opt in with | Reference |
|---|---|---|
| Generate requests, handlers and routes for create/update | `[CrudCreate]` ctor, `[CrudUpdate]` method | [CRUD attributes](crud-attributes.md#the-four-attributes) |
| Generate the DTO from the entity | `[GenerateDto(typeof(Entity))]` on a partial record | [CRUD attributes](crud-attributes.md#the-four-attributes) |
| Non-CRUD domain verbs on their own route (`PUT /{id}/discontinue`) | `[CrudAction]`, with optional segment and verb overrides | [CRUD attributes](crud-attributes.md#domain-actions-with-crudaction) |
| Raise a domain event on save without writing publish code | `[RaisesEvent(EventOperations.X, nameof(Prop))]` | [EF Core domain events](efcore-events.md) |
| Raise a domain event from inside an aggregate method | `AddEvent(...)` in the entity | [EF Core domain events](efcore-events.md) |
| Reject bad input before the handler runs | a FluentValidation `AbstractValidator<TRequest>` — auto-discovered | [API pipeline](api-pipeline.md#fluentvalidation-auto-validation), [Extension points](extension-points.md#requests-handlers-and-validators) |
| Make a replayed `POST` return the first result instead of creating twice | `.RequiredIdempotentKey()` on the route; client sends `X-Idempotency-Key` | [API pipeline](api-pipeline.md#idempotency-on-post) |
| Fill the acting user into a request from the token | `[FromClaim(...)]` on a request property | [API pipeline](api-pipeline.md#fromclaim-population) |
| Stamp the acting user onto every entity without touching requests | `DataOwnerHook` — already wired; nothing per-feature | [Auditing and data ownership](auditing-and-data-ownership.md) |
| Ship reference rows with the feature | a `DataSeedingConfiguration<T>` subclass — auto-discovered | [CRUD attributes](crud-attributes.md#data-seeding) |
| Gapless human-readable numbers (order no., invoice no.) | a sequence definition | [CRUD attributes](crud-attributes.md#sequences) |
| Forward a domain event to an external broker | `Produce`/`Consume` in `ServiceBusSetup` | [SlimMessageBus messaging](slimbus-messaging.md) |

**Read side — shaping the response**

| Capability | Opt in with | Reference |
|---|---|---|
| Reusable, testable query predicates instead of inline `IQueryable` | a `Specification<T>` subclass under `V1/Specs/` | [Querying and specifications](querying-and-specifications.md) |
| Filter, search, order and page a list route | nothing — every generated list route exposes it | [Generic list endpoint](generic-list-endpoint.md) |
| Limit which fields are filterable/searchable | the DTO is the boundary; fields must map to real columns | [Generic list endpoint](generic-list-endpoint.md#the-dto-is-the-boundary) |
| Project to a DTO after `SaveChanges` without an extra round trip | `mapper.ResultOf<T>(entity)` / `LazyMap<T>()` | [DDD implementation guide](ddd-implementation-guide.md) |
| Withhold a property from callers who lack a role | `[SensitiveData("role")]`, or `[SensitiveData]` for any authenticated caller | [CRUD attributes](crud-attributes.md#declaring-a-sensitive-property), [API pipeline](api-pipeline.md#role-aware-sensitive-property-filtering) |
| Return only rows the caller owns | ownership filtering — already wired on `CoreDbContext` | [Auditing and data ownership](auditing-and-data-ownership.md#row-level-ownership-filtering) |
| Aggregate counts by status without loading rows | the status-counts endpoint helper | [Querying and specifications](querying-and-specifications.md#status-counts-endpoint-helper) |

**Protecting and proving the slice**

| Capability | Opt in with | Reference |
|---|---|---|
| Require a token / a role on a route | `RequireAuthorization(...)` on the group or route | [API pipeline](api-pipeline.md#authentication--authorization), [Extension points](extension-points.md#authorization-and-claims) |
| Throttle a route | a named rate-limit policy | [API pipeline](api-pipeline.md#rate-limiting), [Extension points](extension-points.md#rate-limiting) |
| Ship the feature switched off | a `FeatureOptions` property plus the matching `FeatureManagement` JSON key | [Template features](template-features.md#featuremanagement-flags), [Configuration reference](configuration-reference.md) |
| Cover the slice at the right level | handler/`Result` and model assertions in `Minimal.App.Tests`; HTTP behaviour in `Minimal.App.BDDTests` | [DDD implementation guide](ddd-implementation-guide.md) |
| Keep layer references legal | nothing — the architecture tests fail the build for you | [Extension points](extension-points.md#boundaries-your-code-must-respect) |

Two caveats worth reading before you rely on a row above: a DataAnnotations attribute on a generated
request is *forwarded but not enforced* ([why](crud-attributes.md#end-to-end-trace-both-samples)), and
`[SensitiveData]` filters nothing until the serializer opt-in is present — it is already wired in a
generated solution, but that is what makes it work
([why](crud-attributes.md#declaring-is-not-enforcing)).

## Diagrams

Every diagram on these pages is committed twice under [`diagrams/`](diagrams): the typed JSON IR it
was authored from, and the rendered `.svg` the Markdown embeds. Edit the IR, re-render, and commit
both — never hand-edit the SVG.

| Diagram | Shows | Referenced from |
|---|---|---|
| `templates-solution-layers` | Project layers and which way the references point | root `README.md`, [DDD guide](ddd-implementation-guide.md) |
| `templates-request-pipeline` | Every stage a request crosses, and each short-circuit response | root `README.md`, [API pipeline](api-pipeline.md) |
| `templates-domain-event-path` | An event from aggregate to handler, across the save boundary | [EF Core domain events](efcore-events.md) |
| `templates-crud-generation` | Attributes in, generated requests/handlers/routes out | [CRUD attributes](crud-attributes.md) |
| `templates-aspire-topology` | What the Aspire host provisions and injects | [Template features](template-features.md) |

## How the plumbing works

- [API Request Pipeline](api-pipeline.md) — what happens to a request before it reaches a handler.
- [SlimMessageBus Messaging](slimbus-messaging.md) — the command/query/event bus wiring.
- [EF Core Domain Events](efcore-events.md) — how domain events are collected and published.
- [Auditing and Data Ownership](auditing-and-data-ownership.md) — how audit fields get populated
  and can't be forged.

## Worked samples

- [Manual vs. Automated](samples/manual-vs-automated.md) — a layer-by-layer comparison of the two
  sample styles below.
- [Manual sample: Purchase Orders](samples/manual-purchase-orders/README.md) — the full vertical
  slice written by hand.
- [Automated sample: Products](samples/automated-products/README.md) — creation and events
  declared by attribute.
