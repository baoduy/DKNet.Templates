# DKNet.Minimal.Template Documentation

Reference docs for the `DKNet.Minimal.Template` — a NuGet solution template that scaffolds
production-ready .NET 10 microservices using vertical-slice DDD/CQRS.

## Getting started

- [Template Usage Reference](template-usage.md) — install the template, scaffold a solution, then
  run, test, and publish it.
- [Template Feature List](template-features.md) — everything `dotnet new dknet-minimal` wires up
  before you write feature code.
- [DKNet Package Inventory](dknet-packages.md) — the DKNet NuGet family, one package per capability.

## Building a feature

- [DDD Implementation Guide](ddd-implementation-guide.md) — add one vertical-slice feature, entity
  to endpoint.
- [CRUD Attributes](crud-attributes.md) — build a full CRUD slice from four attributes, domain actions included
  (generator-driven).
- [Querying and Specifications](querying-and-specifications.md) — the read side, from HTTP request
  to paged projection.

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
