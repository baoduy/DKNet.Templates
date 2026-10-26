# Purchase Orders

Create, correct, cancel and look up a customer's purchase orders, with a safe-to-retry create call.

## 📖 Overview

- An order can be created, read back, re-priced, cancelled or removed. Once cancelled it stays
  cancelled — no route re-opens it.
- Creating an order is safe to retry: a network retry that repeats the same `X-Idempotency-Key`
  never creates a second order (it is refused instead of silently re-run — see
  [End-to-end flow](#-end-to-end-flow)).
- Every order belongs to the caller who created it. A tenant ownership key, stamped independently of
  "who created it", decides who can read or change the order — a different caller gets a plain
  `404`, not a permission error, and never sees it in their own list.
- Listing can be narrowed to one customer's orders and is paged (20 per page by default, 100 at
  most).
- Called by any authenticated caller of the service — the built-in demonstration identity in
  Development, a real signed-in subject elsewhere (see the open question below on who that caller is
  meant to be in production).

## 🏢 Business domain

This is the template's one fully hand-written vertical slice — every layer from entity to endpoint is
written out, with no `[RaisesEvent]`, `[CrudCreate]`/`[CrudUpdate]`, or `[GenerateDto]` attribute
anywhere in it (contrast: [Automated Products](../automated-products/index.md), the same shape of
feature built from declarative attributes and compile-time generators). It does not overlap
Products: a different aggregate, a different schema (`manual_sample` vs. `sample`), no shared table.

| Term | Meaning | In the code |
|---|---|---|
| Purchase order | One customer's order, tracked through to cancellation | `PurchaseOrder` |
| Placed | The only status a new order is created in | `PurchaseOrderStatus.Placed` |
| Cancelled | Terminal status — no route reopens an order from here | `PurchaseOrderStatus.Cancelled` |
| Ownership key | The caller-scoped key that decides who can read or write an order; independent of "created by" | `PurchaseOrder.OwnedBy` |

| Rule | Enforced by | A caller who breaks it gets |
|---|---|---|
| Create requires an `X-Idempotency-Key` header | `.RequiredIdempotentKey()` on the create route | `400 Bad Request` |
| A replayed create key is refused, not replayed | `IdempotencyEndpointFilter` (`ConflictHandling = ConflictResponse`) | `409 Conflict` |
| Customer name and amount are required on create | `CreatePurchaseOrderCommandValidator` | `400 Bad Request` with field errors |
| Amount must be positive on update | `UpdatePurchaseOrderCommandValidator` | `400 Bad Request` |
| An already-cancelled order cannot be cancelled again | `CancelPurchaseOrderCommandHandler`'s status guard | `409 Conflict` |
| Every write needs a resolvable acting user | each command handler's `string.IsNullOrEmpty(request.ByUser)` guard | `400 Bad Request` |
| A caller can only see and change its own orders | the row-level ownership filter plus `DataOwnerHook` | `404 Not Found` (by-id routes), silently absent from the list |
| An authenticated caller with no ownership key cannot create one | ownership enforcement raised as `OwnershipRequiredException` | `403 Forbidden`, no row persisted |

## 🚀 Quick Start

No separate token step — the Development environment runs with `FeatureManagement:RequireAuthorization`
off and the built-in demonstration identity on, so every call below is already "authenticated" as that
identity. Create one order; `X-Idempotency-Key` is required even on this first call:

```bash
curl -i -X POST http://localhost:5077/v1/purchase-orders \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: 4581d150-d72f-4384-ae88-d6e0341b743d" \
  -d '{"customerName":"Docs Test Co","amount":99.90}'
```

```http
HTTP/1.1 201 Created
Content-Type: application/json; charset=utf-8

{"id":"8daca0f3-a90e-4501-91b5-21cbcb76c8bb","customerName":"Docs Test Co","amount":99.90,"status":"placed","createdBy":"demo-user@not-a-real-identity.invalid"}
```

Run against a local instance. `createdBy` resolves from the caller's claims, never from the request
body — see [Endpoints](endpoints.md) for every other route.

## 🔄 End-to-end flow

![A caller posts a purchase order with an idempotency key; the filter forwards a new key through validation, the aggregate constructor and SaveChanges to the in-memory event consumer, while a replayed key is refused with 409 before the pipeline runs.](diagrams/create-flow.svg)

The create route is the one route in this sample guarded by `.RequiredIdempotentKey()`. A new key
runs the full pipeline — FluentValidation, the `PurchaseOrder` constructor (which raises
`PurchaseOrderCreatedEvent` itself, via `AddEvent`), `SaveChanges`, then the in-memory bus delivers the
event to `PurchaseOrderCreatedEventHandler`, which only logs it. A replayed key short-circuits at the
filter: this deployment configures `ConflictHandling = ConflictResponse`, so the second call never
reaches the validator or handler and answers `409 Conflict`. The diagram cannot show the transaction
boundary (the constructor call, `AddAsync` and `SaveChanges` all run inside one `DbContext` save) or
what happens if the in-memory publish throws after the row has already committed — untested by this
sample.

The order's status changes only once, on cancel:

![A purchase order starts Placed on every constructor and moves to the terminal Cancelled state through Cancel(userId); cancelling it again is refused with 409 rather than looping back to Placed.](diagrams/status-lifecycle.svg)

`PurchaseOrderStatus` also declares `Draft`, but no constructor, handler or migration in this sample
ever assigns it — every order is `Placed` from the moment it exists. The diagram cannot show that
`Delete` works regardless of status; deletion is independent of the lifecycle above.

## 📚 Pages

- [Endpoints](endpoints.md) — every route, its request/response shape, and what can go wrong
- [Data model](data-model.md) — the `PurchaseOrders` table, its columns and its status values
- [Integration](integration.md) — `PurchaseOrderCreatedEvent` and its one consumer

## 🔗 Related features

- [Automated Products](../automated-products/index.md) — copy that sample instead for a plain CRUD
  entity where you don't need idempotent writes, a filtered list, or a precondition the generated
  routes can't express. The full trade-off is
  [Manual vs. Automated — At a glance](../../samples/manual-vs-automated.md#at-a-glance-which-one-should-i-copy).
- `IPurchaseOrdersClient` — a typed Refit client for this feature's routes, registered by
  `AddApiClient`, for a downstream service that wants to call it without hand-rolling HTTP.

## ❓ Open questions

| Question | Why it matters | Checked | Who can answer |
|---|---|---|---|
| Who is this API meant to be called by in production — an internal back-office tool, or a partner integration? | Changes the Overview's "who calls it" line and whether the deployment guide should treat it as externally exposed | README, `docs/index.md`, `docs/template-features.md` — none names an intended caller; the sample ships with the built-in demonstration identity only | Template maintainer / product-owner |
| Is `PurchaseOrderStatus.Draft` reserved for a planned future workflow, or dead code to remove? | Affects whether a future feature should document a create-as-draft path | Searched every assignment to `Status` in `src/ApiEndpoints/` — none sets `Draft` | Template maintainer |
| What does this route return if the idempotency store itself is unavailable (Redis down, with no connection string fallback engaged)? | The deployment guide should say whether a write then fails closed or open | `IdempotencyEndpointFilter`'s own code path for `store.IsKeyProcessedAsync` failing was not exercised against this running instance | DKNet.AspCore.Idempotency maintainer |
