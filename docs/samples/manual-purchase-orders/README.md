# Manual sample: Purchase Orders

> Every layer of this feature — entity, event, event handler, CRUD requests, queries, endpoint
> routes, DTO — is hand-written. No `[RaisesEvent]`, `[CrudCreate]`/`[CrudUpdate]`, or
> `[GenerateDto]` attribute is used anywhere in it.

## What it demonstrates

This sample implements the full vertical-slice pattern by hand, from entity to endpoint. It shows:

- A `PurchaseOrder` aggregate that raises its own creation event via `AddEvent`.
- FluentValidation-backed create and update requests.
- A business rule that rejects cancelling an already-cancelled order.
- A filtered, paged list query.
- Static reference-data seeding.

Which of the two samples to copy for a feature of your own is stated once, in
[Manual vs. Automated — At a glance](../manual-vs-automated.md#at-a-glance-which-one-should-i-copy);
this page does not carry a second version of it. The same document holds the line-by-line trade-off
against the automated sample.

## Routes

Base path `/v1/purchase-orders`:

| Route | Notes |
|---|---|
| `POST /` | Requires `X-Idempotency-Key: {Guid}` header — a replayed key returns the original response instead of creating a duplicate. |
| `GET /` | Paged, optional `CustomerName` filter. |
| `GET /{id}` | 404 on unknown id. |
| `PUT /{id}` | Changes `Amount`; 404 on unknown id. |
| `POST /{id}/cancel` | 400 if already cancelled. |
| `DELETE /{id}` | 404 on unknown id. |

## Platform capabilities it carries

- **Request idempotency** — `.RequiredIdempotentKey()` on the create route.
- **Static seed data** — 3 fixed-id purchase orders via `PurchaseOrderStaticData`, discovered by
  `UseAutoDataSeeding` and visible over `GET /v1/purchase-orders` on a fresh database.

It does **not** carry external broker publish/subscribe. Its domain event stays on the in-memory
bus only; see the automated sample for that capability.

## Deleting this sample

Remove `src/ApiEndpoints/**/ManualSample/**`, `src/ApiEndpoints/Minimal.Api/ApiEndpoints/ManualSample/`,
and this `docs/samples/manual-purchase-orders/` folder. Then drop its EF Core mapping/seed classes
from the next migration. No other feature depends on it.
