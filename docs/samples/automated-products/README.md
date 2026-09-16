# Automated sample: Products

> Every layer the DKNet 10.1.26 generators can produce is declared, not written:
> `[RaisesEvent]` for events, `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` + `[GenerateDto]` for the
> generated request/handler/route/DTO shapes.

## What it demonstrates

This sample declares behavior through attributes instead of writing it by hand. It shows:

- A `Product` aggregate whose creation and price-change events are declared by attribute; no
  `AddEvent` call appears anywhere in this sample.
- Create and update requests, handlers, and routes generated from a `[CrudCreate]` constructor and
  a `[CrudUpdate]` method.
- **Domain actions** declared as `[CrudAction]` methods — `Approve` published at an explicit route
  segment on the default verb, `AssignSupplierReference` overriding both segment and verb, and
  `Discontinue` declaring the verb override — so the sample shows the default and each override
  without you inventing one. The first two are published by the generator with no hand-written
  request, handler or endpoint registration; `Discontinue` is deliberately excluded from the
  generated map by name and hand-written, because discontinuing a product also creates its named
  replacement in the same transaction — and an operation that writes more than one aggregate in one
  transaction cannot be generated. That the hand-written command also refuses a second call is a
  consequence of that rule, not the reason the route left the generated map.
- A **composite endpoint**: `ProductV1Endpoint` calls `MapProductCrud(o => …)` first — per-route
  scopes plus `Exclude("Discontinue")` — and hand-writes only the two routes the generator cannot
  express, below it. Generated and hand-written routes live in the same group, not in rival samples.
- A declaratively raised event that still reaches an external Azure Service Bus topic, through
  hand-wired publish/subscribe.
- Two supplier fields declared `[SensitiveData]` on the entity, withheld from callers who lack the
  role they name — with no second response model and no filtering code in any handler.

For the line-by-line trade-off against the manual sample, including the confirmed validation gap
this sample carries, see [`docs/samples/manual-vs-automated.md`](../manual-vs-automated.md).

## Routes

Base path `/v1/products`. Seven routes come from `MapProductCrud(o => …)`; the last two are
hand-written below that call. The **Scope** column is the scope each route's
`RequireAuthorization` demands — applied only when `FeatureManagement:RequireAuthorization` is on,
so a stock Development run enforces none of it.

| Route | From | Scope | Notes |
|---|---|---|---|
| `POST /` | generated (`Create`) | `products.write` | **No idempotency protection** and **no enforced validation** — see the comparison doc. |
| `GET /` | generated (`GetList`) | `products.read` | Generic list (`DKNet.AspCore.Extensions`' `MapGetList`) with the uniform `filter`/`search`/`orderBy`/`desc`/`pageNumber`/`pageSize` contract, resolved against `ProductDto`. `pageSize` defaults to `1000` with a configurable ceiling of `1000`, and `fromDate`/`toDate` last-activity bounds which, left out, window an audited listing — `Product` is audited — to the last three months. Full contract: [`docs/generic-list-endpoint.md`](../../generic-list-endpoint.md). |
| `GET /{id}` | generated (`GetById`) | `products.read` | 404 on unknown id (generic `MapGetById`). |
| `PUT /{id}` | generated (`ChangePrice`) | `products.write` | Changes `Price`; 404 on unknown id. |
| `DELETE /{id}` | generated (`Delete`) | `products.write` | Generic `MapDeleteById`. |
| `POST /{id}/approval` | generated (`Approve`) | `products.write` | Domain action — `[CrudAction("approval")] Approve(string byUser)`. Body `{ "byUser": "..." }`; `200` + `ProductDto`; 404 on unknown id. |
| `PUT /{id}/supplier-reference` | generated (`AssignSupplierReference`) | `products.supplier` | Domain action overriding both segment and verb — `[CrudAction("supplier-reference", Verb = CrudActionVerb.Put)] AssignSupplierReference(string supplierReferenceCode)`. Body `{ "supplierReferenceCode": "..." }`; `200` + `ProductDto`; 404 on unknown id. Assigns the `[SensitiveData]` property after creation — it is not a create-request field. The only route on this scope — a per-route setting, not a per-kind one. |
| `PUT /{id}/discontinue` | hand-written | `products.discontinue` | Excluded from the generated map by name (`Exclude("Discontinue")`). Body `{ "replacementName", "replacementPrice" }`; discontinues the product and creates its replacement in one transaction. **Not** a repeatable no-op: a second call on an already-discontinued product is a domain failure. |
| `GET /summary` | hand-written | `products.read` | `{ productCount, averagePrice }` across the caller's own products. A shape the generator has none for — hence hand-written. |

## Platform capabilities it carries

- **External broker publish/subscribe** — a `ProductCreatedEvent`, raised purely by
  `[RaisesEvent]` declaration, is produced to the `product-tp` Azure Service Bus topic and consumed
  by `ProductCreatedNotificationHandler` on the `product-sub` subscription.

- **Generated domain actions** — `Approve` and `AssignSupplierReference` are declared as
  `[CrudAction]` methods on the entity and published as routes with no per-action code. `Approve`
  keeps its acting user as a method parameter, so the generated request exposes it as
  caller-supplied — that is this sample's own long-standing choice, unchanged by the actions. See
  [`docs/crud-attributes.md`](../../crud-attributes.md#domain-actions-with-crudaction) for how to
  declare your own.

- **Per-route options on the generated call** — `MapProductCrud` takes a `CrudMapOptions` action, so
  this endpoint attaches a different scope to each generated route and drops one action by name
  rather than accepting the whole generated set as-is. The overloads and the route-naming rule:
  [`docs/crud-attributes.md`](../../crud-attributes.md#the-four-attributes).

- **Role-gated response properties** — `Product` declares
  `[SensitiveData("pricing")] SupplierCostPrice` and `[SensitiveData] SupplierReferenceCode`. The
  DTO generator carries both attributes onto `ProductDto`, and `Minimal.Api` opts its response
  serializer in once at start-up (`HttpContextSensitiveDataPrincipalAccessor` +
  `UseRoleAwareSensitiveData` in `Minimal.Api/Configs/ServiceConfigs.cs`). A caller in the `pricing`
  role gets `supplierCostPrice`; any other authenticated caller gets the payload without it, and an
  unauthenticated caller gets neither property. The two properties are written by different routes —
  `supplierCostPrice` is an optional create-request field, `supplierReferenceCode` is assigned only
  by the `PUT /{id}/supplier-reference` action above. Declaring:
  [`docs/crud-attributes.md`](../../crud-attributes.md#declaring-a-sensitive-property); the host
  opt-in and its rules:
  [`docs/api-pipeline.md`](../../api-pipeline.md#role-aware-sensitive-property-filtering).

  To watch it happen on a host you just started, use the one seeded product that carries both values.
  `Minimal.AppHost/SampleData/SampleDataGenerator.cs` gives exactly one generated product the fixed name
  **`Demo-Product-With-Supplier-Data`**, with `supplierCostPrice` `42.50` and
  `supplierReferenceCode` `DEMO-SUPPLIER-REF`; every other generated product leaves both
  properties null, so an empty payload on a random row proves nothing either way. Fetch it with
  `GET /v1/products?filter=Name:Equal:Demo-Product-With-Supplier-Data`, and the item in the page
  reads:

  | Caller | `supplierCostPrice` | `supplierReferenceCode` |
  |---|---|---|
  | Unauthenticated — what a stock `dotnet run` gives you | absent | absent |
  | Authenticated, without the `pricing` role | absent | `"DEMO-SUPPLIER-REF"` |
  | Authenticated, in the `pricing` role | `42.50` | `"DEMO-SUPPLIER-REF"` |

  The first row is the one you will hit first, and it is the filtering working rather than failing:
  `FeatureManagement:RequireAuthorization` is `false` in `Minimal.Api/appsettings.Development.json`, so no
  authentication middleware runs, and the filter fails closed on an unauthenticated principal — both
  properties are withheld, the role-less one included. Reaching the other two rows means wiring an
  identity provider, which the template deliberately ships only as a placeholder
  ([`docs/template-usage.md`](../../template-usage.md#what-you-must-change-before-shipping)). *Absent*
  means absent: the property is missing from the JSON object, not `null` and not masked.

  The opt-in is one serializer setting for the whole service, but it only acts on declared
  properties. The hand-written purchase-order sample declares nothing sensitive, so its responses are
  byte-for-byte what they were before the opt-in existed — the filtering costs nothing where nothing
  is declared.

It does **not** carry request idempotency, nor any migration-time static seed data — no `Product` row is
written by the seeding that runs with the migration, the way the manual sample's
`Minimal.Infra/Features/ManualSample/StaticData/PurchaseOrderStaticData.cs` writes its three reference
purchase orders; see the manual sample for both. The demonstration product above is not a counter-example:
it is generated at run time by the Aspire application host, only when `SampleData:RecordsPerEntity` is
above zero, and it is absent from a database the API migrated on its own.

Its forwarded `[Range]` validation on `Price` is never evaluated under this template's own
endpoint-registration convention. Confirmed live: `POST /v1/products` with a negative price returns
`201`, not `400`. Read why in the comparison doc before reusing this pattern for an entity whose
validation must actually be enforced.

## Deleting this sample

Remove `src/ApiEndpoints/**/AutomatedSample/**`, `src/ApiEndpoints/Minimal.Api/ApiEndpoints/AutomatedSample/`,
the two `Produce<ProductCreatedEvent>`/`Consume<ProductCreatedEvent>` lines in
`Minimal.Infra/Extensions/ServiceBusSetup.cs`, and this `docs/samples/automated-products/` folder.
Then drop its EF Core mapping from the next migration. No other feature depends on it.
