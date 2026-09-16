# DKNet API Solution Template

[![NuGet](https://img.shields.io/nuget/v/DKNet.Minimal.Template.svg)](https://www.nuget.org/packages/DKNet.Minimal.Template)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

`dotnet new` solution template for an ASP.NET Core microservice built as vertical slices over
DDD/CQRS — for a team starting a new .NET 10 service that wants the layering, the hardening and the
CRUD plumbing decided before the first feature is written.

Take it when your service is a handful of aggregates behind an HTTP API, persisted in PostgreSQL and
run under .NET Aspire. Skip it if you need a different persistence story or a single-project API —
the layer boundaries here are enforced by tests, not suggestions.

## Why take it

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

## Quick start

```bash
# 1. add the feed, install the template
dotnet nuget add source --username <GITHUB_USERNAME> --password <GITHUB_PAT_WITH_READ_PACKAGES> \
  --store-password-in-clear-text --name github "https://nuget.pkg.github.com/baoduy/index.json"
dotnet new install DKNet.Minimal.Template --nuget-source "https://nuget.pkg.github.com/baoduy/index.json"

# 2. scaffold (a dotted name works: -n DKNet.Accounts)
dotnet new dknet-minimal -n MyCompany.MyService
cd MyCompany.MyService

# 3. run the full stack — Aspire provisions Redis and PostgreSQL via Docker
dotnet run --project MyCompany.MyService.ApiEndpoints/MyCompany.MyService.AppHost
```

`dotnet run --project <Name>.ApiEndpoints/<Name>.Api` starts the API alone, without containers.

> **`--TenantId` and `--ApiAudience` ship as placeholders, not working values.** Left as shipped, the
> OIDC metadata fetch runs against a tenant that does not exist and every token is rejected on
> audience mismatch. Replace them before you turn on `FeatureManagement:RequireAuthorization` — see
> [what you must change before shipping](docs/template-usage.md#what-you-must-change-before-shipping).

Every scaffold parameter, plus the run/test/migrate/pack commands:
[`docs/template-usage.md`](docs/template-usage.md).

## The shape your endpoints should take

**Composite-first.** One endpoint class per aggregate: map the generated CRUD composite at the top,
each generated route carrying its own authorization, then hand-write below it only the routes that
carry real orchestration. Generated and hand-written routes live in the same group — this is not a
choice between two styles of endpoint.

`ProductV1Endpoint` (`Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs`) is that shape,
trimmed here to its structure:

```csharp
public void Map(RouteGroupBuilder group)
{
    group.MapProductCrud(o =>
    {
        o.Exclude("Discontinue");                                            // hand-written below

        o.Configure(CrudOp.GetById, rb => rb.RequireAuthorization(ProductScopes.Read));
        o.Configure(CrudOp.Create,  rb => rb.RequireAuthorization(ProductScopes.Write));
        o.Configure("Approve",      rb => rb.RequireAuthorization(ProductScopes.Write));
        // its own scope — holding products.write must not be enough to assign a supplier reference
        o.Configure("AssignSupplierReference", rb => rb.RequireAuthorization(ProductScopes.Supplier));
    });

    // writes two aggregates in one transaction — outside what the generator can express
    group.MapPut("{id:guid}/discontinue", /* ... */).Produces<ProductDto>();

    // a response shape the generator has none for
    group.MapGet("summary", /* ... */).Produces<ProductPriceSummaryDto>();
}
```

Seven of the nine routes come from `MapProductCrud`. Scopes are applied only when
`FeatureManagement:RequireAuthorization` is on, so a stock Development run enforces none of them.

### When an operation needs a hand-written route

The criterion is stated once, in
[Manual vs. Automated — At a glance](docs/samples/manual-vs-automated.md#at-a-glance-which-one-should-i-copy),
and this README does not carry a second version of it. Read it before you hand-write a handler: the
list is shorter than most readers expect, and two of the cases that look like they belong on it do
not.

- **A rule that refuses an operation after reading stored data does not force a hand-written route.**
  A FluentValidation validator written against the *generated* request runs on the generated route —
  the group-level `AddFluentValidationAutoValidation()` filter applies to every route in the group.
  `CreateProductRequestValidator` refuses a name already taken and `DeleteProductRequestValidator`
  refuses a product still for sale, both with `409`, and neither operation has a hand-written route,
  request or handler.
- **A response value the generator's naming convention cannot produce does not force one either.**
  `ProductDto.GrossMargin` (`Price` minus `SupplierCostPrice`) is derived from two columns, so the
  convention cannot emit it — one property on the generated `partial record` plus a Mapster
  `IRegister` puts it on the response, with no route, request or handler written for it. See
  [a response value the convention cannot produce](docs/samples/automated-products/README.md#a-response-value-the-convention-cannot-produce).

Both samples ship in full: [`docs/samples/automated-products/`](docs/samples/automated-products/)
(generator-driven `Product`) and
[`docs/samples/manual-purchase-orders/`](docs/samples/manual-purchase-orders/) (hand-written
`PurchaseOrder`). The end-to-end steps for a feature of your own are in
[`docs/ddd-implementation-guide.md`](docs/ddd-implementation-guide.md).

## Architecture

A generated solution is layered onion-style — each layer knows only about the layers inside it:

![Architecture diagram of a scaffolded solution: Minimal.AppHost orchestrates Redis and PostgreSQL and starts Minimal.Api, which dispatches through IMessageBus into Minimal.AppServices; AppServices calls Minimal.Domains aggregates, Minimal.Infra supplies the repository and event publisher to AppServices and the EF Core mapping and seeding to Domains, Minimal.Share is read by every layer, and the Minimal.App.Tests/Architecture project holds NetArchTest shape rules over the Api, AppServices, Infra and Domains projects.](docs/diagrams/templates-solution-layers.svg)

`Minimal.Domains` references nothing above it. `Minimal.AppServices` depends only on `Domains` (plus
`Share`); `Minimal.Infra` and `Minimal.Api` depend on both, never the reverse. `Minimal.Share` is the
one cross-cutting exception, and `Minimal.AppHost` is the Aspire orchestrator, carrying no business
logic.

![Workflow diagram of the request pipeline: a request passes the edge middleware that applies forwarded headers, security response headers and CORS, then routing with the request bounds and the rate limiter, then authentication with its default-deny fallback, then the endpoint filters that populate FromClaim members and run FluentValidation, and finally the handler; opt-in routes take a detour through the idempotency filter, and each stage has its own short-circuit response — 413 for an oversized body, 429 or 504, 401 or 403, 400, and the 500 problem+json the global exception handler writes.](docs/diagrams/templates-request-pipeline.svg)

A request reaches an `IEndpointConfig` route, is validated and has its `[FromClaim]` properties
populated, then dispatches over the in-memory SlimMessageBus to a handler in `Minimal.AppServices`,
which calls a method on a `Minimal.Domains` aggregate. `CoreDbContext.SaveChanges` persists it;
around the save, `DataOwnerHook` stamps `CreatedBy`/`UpdatedBy` and queued domain events are
dispatched — and forwarded to Azure Service Bus when one is configured. Stage by stage:
[`docs/api-pipeline.md`](docs/api-pipeline.md).

## Documentation

**Start at [`docs/index.md`](docs/index.md)** — it indexes every page and carries the
capability-to-attribute tables ("I want X; which attribute or call gives it to me").

| First stop | For |
|---|---|
| [`docs/template-usage.md`](docs/template-usage.md) | Scaffold parameters, run/test/migrate/publish |
| [`docs/ddd-implementation-guide.md`](docs/ddd-implementation-guide.md) | Adding a vertical slice, entity to endpoint |
| [`docs/crud-attributes.md`](docs/crud-attributes.md) | The four attributes behind the generated slice |
| [`docs/samples/manual-vs-automated.md`](docs/samples/manual-vs-automated.md) | Which sample to copy, and every trade-off behind that call |
| [`docs/configuration-reference.md`](docs/configuration-reference.md) | Every `appsettings` key a generated solution reads |
| [`docs/extension-points.md`](docs/extension-points.md) | Where your own code attaches, and the boundaries the tests hold |

[AGENTS.md](AGENTS.md) is the condensed architecture reference used by AI coding agents.

> These guides are visible on GitHub but are **not** packaged into scaffolded solutions — the
> nuspec's file list doesn't ship `docs/`. Copy what you need into the generated solution, or add it
> to the nuspec.

## AI assistant plugins

The repo ships a Claude Code plugin and a GitHub Copilot plugin that drive vertical-slice features
end to end. `dotnet new dknet-minimal` copies both folders (`.claude/`, `.claude-plugin/`,
`.github/`) into the generated solution, so your team gets the same agents, skills and commands with
no extra install step.

```text
# Claude Code
/plugin marketplace add baoduy/dknet.templates
/plugin install dknet-minimal

# GitHub Copilot
copilot plugin marketplace add baoduy/DKNet.Templates
```

| Command | Purpose |
|---|---|
| `/dknet-feature <Feature> <Entity> [props…]` | Full vertical slice: plan → entity → CRUD → endpoint → tests → BDD → docs |
| `/dknet-entity <Feature> <Entity> [props…]` | Domain entity + EF mapper + migration |
| `/dknet-crud <Feature> <Entity>` | AppServices CRUD (DTO + Create/Update/Delete + spec + event) |
| `/dknet-endpoint <Feature> <Entity>` | Minimal API `IEndpointConfig` with idempotency on POST |
| `/dknet-unit-tests <Feature> <Entity>` | `ApiFixture` + `IMessageBus` integration tests |
| `/dknet-bdd-test <Feature> <Entity>` | Reqnroll + NUnit BDD scenarios |
| `/dknet-docs <Feature>` | Feature documentation under `docs/features/<feature>/` |

Subagents (`dknet-architect`, `dknet-implementer`, `dknet-bdd-engineer`) and ten domain skills back
the commands — see `.claude/agents/` and `.claude/skills/`. Copilot auto-discovers `.github/agents/`
and `.github/skills/`; the full skill list is in `.github/skills/CATALOG.md`.

## Release notes

**Breaking change — status-counts default window (DRK-521).** Status-counts endpoints (the `GET`
route `MapGetStatusCounts<TEntity>` maps, at the `status` segment by default) no longer default to
the last 30 days when no `from`/`to` bounds are supplied. An unbounded call now reports counts over
the **entire history**. Explicit `from`/`to` bounds are unaffected — pass them to preserve a bounded
window.

## License

MIT © [Steven Hoang](https://drunkcoding.net)
