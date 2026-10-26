# Automated Products

A caller can create, price, approve, discontinue and list the sellable products in their own catalogue.

## 📖 Overview

- A caller creates a product with a unique name, and the catalogue refuses a duplicate name before it is ever written (price is not actually enforced positive — see [Endpoints](endpoints.md)).
- A caller changes a product's price, approves it under an acting user, and assigns a supplier reference — each a distinct, separately scoped operation, not one generic "update."
- A product must be discontinued before it can be deleted — deleting one still for sale is refused, and discontinuing also creates its named replacement in the same transaction.
- A caller in the `pricing` role sees the supplier's confidential cost price and the gross margin it implies; every other caller does not, even on the same row.
- A caller only ever sees the products it owns — creating, listing, and the summary totals are all scoped to the caller's own catalogue, never another caller's. The name-uniqueness rule is the one exception: it holds across every caller, not just the caller's own catalogue, because the database's unique index enforces it with no ownership scope.
- End users and integrators call it directly over HTTP. This feature is also this template's worked example of its generator-driven CRUD approach — most of its behaviour is declared by attribute on `Product` rather than hand-written; only discontinuing and the price summary are hand-written, because neither has a generated shape. See [Manual vs. Automated](../../samples/manual-vs-automated.md#at-a-glance-which-one-should-i-copy) for when to copy this shape instead of the hand-written one.

## 🏢 Business domain

This is a catalogue-management slice: it owns a product's identity, price and sale status, not fulfilment, inventory counts or supplier onboarding (the supplier fields it holds are reference data disclosed only to a confidential role, not a supplier-relationship feature in their own right).

| Term | Meaning | In the code |
|---|---|---|
| Product | A sellable catalogue item: name, price, discontinued flag, optional supplier fields | `Product` entity |
| Discontinue | Retire a product from sale and name its replacement, in one transaction | `Product.Discontinue()` + `DiscontinueProductCommandHandler` |
| Approve | Record that a product was reviewed, stamping the acting user | `Product.Approve(string byUser)` |
| Gross margin | `Price` minus the confidential supplier cost | `ProductDto.GrossMargin` / `ProductMappingRegister` |
| Sensitive data | A field withheld from a caller who lacks the role it names | `[SensitiveData]` on `SupplierCostPrice`, `SupplierReferenceCode`, `GrossMargin` |
| Ownership | The caller identity a product is stamped with at creation; every read is scoped to it | `Product : IOwnedBy`, `OwnedBy` column, `DataOwnerHook` |

| Rule | Enforced by | A caller who breaks it gets |
|---|---|---|
| A product name is unique across *every* caller, even though a caller can only read its own products | `CreateProductRequestValidator`'s read is scoped to the caller's own rows by the ownership filter, same as any other read — it only catches a name taken by **that caller**; the database's unique index on `Name` has no such scope and is the actual cross-caller guarantee | `409 precondition.product-name-taken` when the caller's own name collides; a `500` (not `409`) when the colliding name belongs to a product the caller cannot see — the validator passes it, and the database's index is what refuses it |
| A product must be discontinued before it can be deleted | `DeleteProductRequestValidator` | `409 precondition.product-delete-while-for-sale` |
| A product cannot be discontinued twice | `DiscontinueProductCommandHandler` | `400` ("already discontinued") |
| The supplier cost price and gross margin are visible only to the `pricing` role | `[SensitiveData("pricing")]` + the host's role-aware response filter | The key is absent from the JSON body, not `null` |
| A caller only reads products it owns | The global `IOwnedBy` row filter, scoped from `OwnedBy` | The product behaves as if it does not exist — `404` by id, absent from the list — confirmed by `ProductOwnershipIsolationTests` across two distinct callers sharing one database |

## 🚀 Quick Start

Start the API as the service README describes, then set `BASE` to wherever it is listening (the examples on this page were captured against `http://localhost:5077`). No auth token is needed against a Development instance (`FeatureManagement:RequireAuthorization=false`); the built-in demonstration authenticator attaches a fixed identity to every request regardless (see ❓ Open questions).

```bash
BASE=http://localhost:5077
curl -s -i -X POST "$BASE/v1/products" \
  -H "Content-Type: application/json" \
  -d '{"name":"Docs-Widget-A","price":19.99}'
```

```
HTTP/1.1 201 Created
Location: /
Content-Type: application/json; charset=utf-8
(abridged — security-header response lines omitted)

{"name":"Docs-Widget-A","price":19.99,"isDiscontinued":false,"supplierReferenceCode":null,
 "createdBy":"System","createdOn":"2026-10-26T04:59:24.068094+00:00",
 "id":"01a17693-dc06-7cf5-b296-8ec04941f254"}
```

With `RequireAuthorization=true`, the same call additionally needs a bearer token whose claims satisfy the `products.write` policy — see [Endpoints](endpoints.md).

## 🔄 End-to-end flow

![A caller posts a new product; CreateProductRequestValidator refuses a taken name with 409, otherwise the generated handler inserts the row and the EF save hook raises ProductCreatedEvent to an in-memory handler and, only when EnableServiceBus is on, an Azure Service Bus topic.](diagrams/create-flow.svg)

This diagram cannot show the `[Range(0.01, double.MaxValue)]` gap: nothing on this generated route evaluates it, so a negative price reaches the insert and still returns `201` (confirmed live). It also collapses the Azure branch to "if enabled" — Development ships `EnableServiceBus=false`, so only the in-memory handler actually runs there.

![Product moves from for-sale to discontinued to deleted; a second discontinue call and a delete while still for sale are refused, not drawn as transitions.](diagrams/lifecycle.svg)

This diagram cannot show that discontinuing also inserts a second, independent `Product` row (the named replacement) in the same `SaveChangesAsync` call — that new row starts its own lifecycle at "For sale." See [the discontinue route](endpoints.md#put-iddiscontinue) for that sequence.

## 📚 Pages

- [Endpoints](endpoints.md) — every route, its scope, request/response shape and the errors it can return
- [Data model](data-model.md) — the `sample.Products` table and the product's status values
- [Integration](integration.md) — the three declared events and the Azure Service Bus topic/subscription

## 🔗 Related features

- [Manual Purchase Orders](../manual-purchase-orders/index.md) — the hand-written twin of this same vertical-slice shape; reach for it instead when a feature needs idempotent writes, enforced `DataAnnotations` validation, a multi-aggregate transaction expressed end to end by hand, or a filtered list query beyond the generic contract (see [Manual vs. Automated](../../samples/manual-vs-automated.md#at-a-glance-which-one-should-i-copy)).

## ❓ Open questions

| Question | Why it matters | Checked | Who can answer |
|---|---|---|---|
| In Development, the demonstration authenticator (`DemoAuthenticationHandler`) issues every caller an authenticated identity with no role claims — so `supplierReferenceCode` is present (any authenticated caller) but `supplierCostPrice`/`grossMargin` are absent (no `pricing` role). The old sample page described a stock Development run as *unauthenticated*, with both fields absent. Which is the intended default behaviour for a template a consumer runs unmodified? | Affects what a developer should expect to see on first run, and whether the comparison doc needs correcting. | Read `DemoAuthConfig.cs` in full; confirmed live that `supplierReferenceCode` is present and `supplierCostPrice` is absent on an unmodified `dotnet run`. | Whoever owns `docs/samples/automated-products/README.md` and `docs/api-pipeline.md`'s role-aware filtering section. |
| Does the hybrid message bus publish `ProductCreatedEvent` to the in-memory bus and the Azure topic atomically, or could one succeed while the other fails? Does publish happen after the database commit with no outbox, so a crash between commit and publish drops the event? | Determines whether "the event always reaches its consumers" can be stated as a guarantee anywhere in this feature's docs. | Read `ServiceBusSetup.AddServiceBus`/`AddAzureBus` and `EfAutoSavePostInterceptor`; the actual publish-ordering and outbox behaviour live in the `SlimMessageBus`/DKNet.SlimBus.Extensions packages, outside this repository. | A maintainer of `DKNet.SlimBus.Extensions`. |
| With `FeatureManagement:EnableServiceBus` on and the `product-tp` topic unreachable, does `POST /` still answer `201` (publish failure swallowed) or does the request fail after the row has already committed? | Determines whether a caller can rely on a `201` response even when the external broker is degraded. | Read `ServiceBusSetup.AddAzureBus`; `TopologyProvisioning.Enabled = false` means this code never creates the topic, and Development always ships `EnableServiceBus=false`, so this path cannot be exercised against this running instance. | Whoever runs this feature with `EnableServiceBus=true` against a real or emulated Azure Service Bus. |
| `PUT /{id}/discontinue`'s replacement-name-collision `500` returned a full exception stack trace against this Development instance. Does a non-Development environment suppress that detail, and if so, through which setting? | Determines whether the stack-trace body documented in [Endpoints](endpoints.md#put-iddiscontinue) is Development-only or ships to every environment. | Traced the error pipeline to `AddErrorResponses(...)` in `FluentValidationConfig.cs`, which wires `UseExceptionHandler()`; the environment-conditional detail setting, if any, was not found in this repository. | Whoever owns `Minimal.Api/Configs/FluentValidationConfig.cs` or the `DKNet.AspCore.Extensions` package it calls into. |
| `AddMemoryBus` sets `EnableBlockingPublish = false` on the in-memory child bus. Does that mean a handler exception for `ProductCreatedEvent` is swallowed rather than surfaced to the `POST /` caller? | Determines whether a bug in `ProductCreatedEventHandler` could ever fail a create request. | Read the `EnableBlockingPublish` call site in `ServiceBusSetup.AddMemoryBus`; what the flag actually changes lives in the `SlimMessageBus` provider package, outside this repository. | A maintainer of `SlimMessageBus`'s memory provider. |
