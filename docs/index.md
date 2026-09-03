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
