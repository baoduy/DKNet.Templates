# DKNet.Minimal.Template Documentation

Reference docs for the `DKNet.Minimal.Template` — a NuGet solution template that scaffolds
production-ready .NET 10 microservices using vertical-slice DDD/CQRS.

## Why take it

Take it when your service is a handful of aggregates behind an HTTP API, persisted in PostgreSQL and
run under .NET Aspire. Skip it if you need a different persistence story or a single-project API —
the layer boundaries here are enforced by tests, not suggestions.

- **The layering is held by the build, not by review.** Every project reference points inward, so an
  outward one is a circular reference MSBuild refuses; NetArchTest rules in
  `Minimal.App.Tests/Architecture/` fail the test run on the shape rules a compiler cannot see —
  `internal sealed` endpoints and handlers, an explicit max length on every mapped string,
  `HasConversion<string>()` on every mapped enum, Npgsql-only packages.
- **The HTTP surface ships hardened.** Default-deny authorization, security response headers, stated
  request bounds (30 s / 1 MB / 10 s), a status-only public health probe, an enumerated CORS
  allow-list, a non-root image, and a dependency audit that fails the build — all on in the base
  `appsettings.json` and relaxable for local work by configuration alone.
- **Most CRUD is declared, not written.** Four attributes produce the requests, handlers, routes and
  DTO for an aggregate, domain actions included — so you write the aggregate and the operations that
  actually carry business orchestration.
- **Both test levels are already scaffolded** — xUnit + Shouldly unit/integration tests, and
  Reqnroll + NUnit BDD tests against a real host.
- **One image, one entry point.** With no argument the service serves; with a registered job name it
  runs that job and exits on its own status, so the same image migrates the database as a Kubernetes
  `Job` and serves as a `Deployment`.

## Architecture of a generated solution

A generated solution is layered onion-style — each layer knows only about the layers inside it:

![Architecture diagram of a scaffolded solution: Minimal.AppHost orchestrates Redis and PostgreSQL and starts Minimal.Api, which dispatches through IMessageBus into Minimal.AppServices; AppServices calls Minimal.Domains aggregates, Minimal.Infra supplies the repository and event publisher to AppServices and the EF Core mapping and seeding to Domains, Minimal.Share is read by every layer, and the Minimal.App.Tests/Architecture project holds NetArchTest shape rules over the Api, AppServices, Infra and Domains projects.](diagrams/templates-solution-layers.svg)

`Minimal.Domains` references nothing above it. `Minimal.AppServices` depends only on `Domains` (plus
`Share`); `Minimal.Infra` and `Minimal.Api` depend on both, never the reverse. `Minimal.Share` is the
one cross-cutting exception, and `Minimal.AppHost` is the Aspire orchestrator, carrying no business
logic.

![Workflow diagram of the request pipeline: a request passes the edge middleware that applies forwarded headers, security response headers and CORS, then routing with the request bounds and the rate limiter, then authentication with its default-deny fallback, then the endpoint filters that populate FromClaim members and run FluentValidation, and finally the handler; opt-in routes take a detour through the idempotency filter, and each stage has its own short-circuit response — 413 for an oversized body, 429 or 504, 401 or 403, 400, and the 500 problem+json the library's error handler writes.](diagrams/templates-request-pipeline.svg)

A request reaches an `IEndpointConfig` route, is validated and has its `[FromClaim]` properties
populated, then dispatches over the in-memory SlimMessageBus to a handler in `Minimal.AppServices`,
which calls a method on a `Minimal.Domains` aggregate. `CoreDbContext.SaveChanges` persists it;
around the save, `DataOwnerHook` stamps `CreatedBy`/`UpdatedBy` and queued domain events are
dispatched — and forwarded to Azure Service Bus when one is configured. Stage by stage:
[API Request Pipeline](api-pipeline.md).

## Getting started

- [Template Usage Reference](template-usage.md) — install the template, scaffold a solution, then
  run, test, and publish it.
- [Template Feature List](template-features.md) — everything `dotnet new dknet-minimal` wires up
  before you write feature code.
- [DKNet Package Inventory](dknet-packages.md) — the DKNet NuGet family, one package per capability.
- [Configuration](configuration.md) — every `appsettings` key a generated solution reads: type,
  default, rules, when it takes effect, and what it changes.
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
| Non-CRUD domain verbs on their own route (`PUT /{id}/supplier-reference`) | `[CrudAction]`, with optional segment and verb overrides | [CRUD attributes](crud-attributes.md#domain-actions-with-crudaction) |
| Raise a domain event on save without writing publish code | `[RaisesEvent(EventOperations.X, nameof(Prop))]` | [EF Core domain events](efcore-events.md) |
| Raise a domain event from inside an aggregate method | `AddEvent(...)` in the entity | [EF Core domain events](efcore-events.md) |
| Reject bad input before the handler runs | a FluentValidation `AbstractValidator<TRequest>` — auto-discovered | [API pipeline](api-pipeline.md#fluentvalidation-auto-validation), [Extension points](extension-points.md#requests-handlers-and-validators) |
| Refuse a replayed `POST` (`409`) instead of creating twice | `.RequiredIdempotentKey()` on the route; client sends `X-Idempotency-Key` | [API pipeline](api-pipeline.md#idempotency-on-post) |
| Fill the acting user into a request from the token | `[FromClaim(...)]` on a request property | [API pipeline](api-pipeline.md#fromclaim-population) |
| Stamp the acting user onto every entity without touching requests | the `DKNet.EfCore.AuditLogs` hook — already wired; nothing per-feature | [Auditing and data ownership](auditing-and-data-ownership.md) |
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
| Ship the feature switched off | a `FeatureOptions` property plus the matching `FeatureManagement` JSON key | [Template features](template-features.md#featuremanagement-flags), [Configuration](configuration.md) |
| Cover the slice at the right level | handler/`Result` and model assertions in `Minimal.App.Tests`; HTTP behaviour in `Minimal.App.BDDTests` | [DDD implementation guide](ddd-implementation-guide.md) |
| Keep layer references legal | nothing — the architecture tests fail the build for you | [Extension points](extension-points.md#boundaries-your-code-must-respect) |

Two caveats worth reading before you rely on a row above: a DataAnnotations attribute on a generated
request is *forwarded but not enforced* ([why](crud-attributes.md#end-to-end-trace-both-samples)), and
`[SensitiveData]` filters nothing until the serializer opt-in is present — it is already wired in a
generated solution, but that is what makes it work
([why](crud-attributes.md#declaring-is-not-enforcing)).

## Diagrams

Every repo-wide diagram on these pages is committed twice under [`diagrams/`](diagrams) (a feature's own
diagrams sit in its `features/<feature>/diagrams/` folder): the typed JSON IR it
was authored from, and the rendered `.svg` the Markdown embeds. Edit the IR, re-render, and commit
both — never hand-edit the SVG.

| Diagram | Shows | Referenced from |
|---|---|---|
| `runtime` | The running service, its stores and external dependencies, and its trust boundary | root `README.md` |
| `templates-solution-layers` | Project layers and which way the references point | this page, [DDD guide](ddd-implementation-guide.md) |
| `templates-request-pipeline` | Every stage a request crosses, and each short-circuit response | this page, [API pipeline](api-pipeline.md) |
| `templates-domain-event-path` | An event from aggregate to handler, across the save boundary | [EF Core domain events](efcore-events.md) |
| `templates-crud-generation` | Attributes in, generated requests/handlers/routes out | [CRUD attributes](crud-attributes.md) |
| `templates-aspire-topology` | What the Aspire host provisions and injects | [Template features](template-features.md) |
| `templates-launch-mode` | How process arguments decide between serving, running a job, and failing | [Template usage](template-usage.md#launch-mode-serve-or-run-a-job) |

## How the plumbing works

- [API Request Pipeline](api-pipeline.md) — what happens to a request before it reaches a handler.
- [SlimMessageBus Messaging](slimbus-messaging.md) — the command/query/event bus wiring.
- [EF Core Domain Events](efcore-events.md) — how domain events are collected and published.
- [Auditing and Data Ownership](auditing-and-data-ownership.md) — how audit fields get populated
  and can't be forged.

## Worked samples

- [Manual vs. Automated](samples/manual-vs-automated.md) — a layer-by-layer comparison of the two
  sample styles below.
- [Purchase Orders](features/manual-purchase-orders/index.md) — the manual sample: the full vertical
  slice written by hand.
- [Products](features/automated-products/index.md) — the automated sample: creation and events
  declared by attribute.
- [The shape your endpoints should take](endpoint-shape.md) — one endpoint class per aggregate, the
  generated composite first, hand-written routes only where they carry orchestration.
- [Deleting the samples](deleting-the-samples.md) — what to remove, per sample, once you have copied
  what you need.

## Repository

- [Runtime architecture](../README.md#-runtime-architecture) — the running service, its stores and
  its trust boundary, in the repository README.
- [AI plugin](ai-plugin.md) — the `dknet-minimal` skills and subagents, their workflows, and how the
  plugin is released.
- [Release notes](release-notes.md) — behaviour changes a consumer must act on.
- [AGENTS.md](../AGENTS.md) — the condensed architecture reference used by AI coding agents; the one
  guide that ships inside a generated solution.

> These guides are visible on GitHub but are **not** packaged into scaffolded solutions — the
> nuspec's file list doesn't ship `docs/`. Copy what you need into the generated solution, or add it
> to the nuspec.

## License

MIT © [Steven Hoang](https://drunkcoding.net) — see [LICENSE](../LICENSE).
