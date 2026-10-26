# Automated Products — Endpoints

Part of [Automated Products](index.md).

Base path `/v1/products`. Seven routes come from `MapProductCrud(o => …)`; `PUT /{id}/discontinue` and `GET /summary` are hand-written below that call in `ProductV1Endpoint`. A scope in the table below is enforced only when `FeatureManagement:RequireAuthorization` is on — a stock Development run (`RequireAuthorization: false`) enforces none of it, though the built-in demonstration authenticator still attaches an identity with no role claims to every request (see ❓ Open questions on [index.md](index.md)).

## 🔌 Endpoints

| Verb | Path | Purpose | Auth |
|---|---|---|---|
| `POST` | `/` | Create a product | `products.write` |
| `GET` | `/` | List products | `products.read` |
| `GET` | `/{id}` | Get a product by id | `products.read` |
| `PUT` | `/{id}` | Change price | `products.write` |
| `DELETE` | `/{id}` | Delete a product | `products.write` |
| `POST` | `/{id}/approval` | Approve a product | `products.write` |
| `PUT` | `/{id}/supplier-reference` | Assign the supplier reference | `products.supplier` |
| `PUT` | `/{id}/discontinue` | Discontinue and replace | `products.discontinue` |
| `GET` | `/summary` | Product count and average price | `products.read` |

### `POST /`

Creates a product. Generated from the `[CrudCreate]` constructor on `Product`.

- **Auth:** `products.write` — declared once for the whole group (`POST`/`PUT`/`DELETE`) by `[EndpointGroupScope]` on `ProductV1Endpoint`.
- **Idempotency:** not idempotent. The generated route adds no `.RequiredIdempotentKey()` filter, so there is no key the caller can replay to get the original response back. If a `201` response is lost after the insert committed, retrying the identical request gets `409` (the name is now taken), not the original `201` — the caller has to `GET` by name to learn the first call succeeded. A retry with a **different** name always creates a second, distinct product.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `name` | string | yes | `[Required]`, `[StringLength(150)]` declared on the generated request — **neither is evaluated** on this route (see below) | body |
  | `price` | decimal | yes | `[Range(0.01, double.MaxValue)]` declared, **never evaluated** | body |
  | `supplierCostPrice` | decimal? | no | none | body |

  `CreateProductRequestValidator` adds exactly one rule of its own — the name-uniqueness check below — and nothing for `price` or length. An empty `name` (confirmed live) and a 151-character `name` both bypass validation: the empty name creates successfully (`201`), and the over-length name reaches the database's `varchar(150)` column and fails there with `500` (a `DbUpdateException`), not a `400`.

- **Response:** `201 Created`, `Location: /`, body is `ProductDto`.
- **Errors:**

  | Status | Code | When |
  |---|---|---|
  | `409` | `precondition.product-name-taken` | `CreateProductRequestValidator` finds the name already taken **by one of the caller's own products** — its read is scoped by the same global ownership filter as every other read. A check, not a guarantee even within that scope: two callers can pass it at the same instant and both insert — the database's own unique index on `Name` is the last word; the validator exists to turn the common case into a clear `409` instead of a raw constraint violation. **Copy both halves, never the validator alone**, when reusing this pattern. |
  | `500` | — | A name over 150 characters, or any other value the database rejects but no validator checks (confirmed live). The same path also covers a name already taken by a product owned by a **different** caller: invisible to the validator's ownership-scoped read, so it passes validation and fails only at the database's unique index — confirmed live by inserting a row directly under a different `OwnedBy` value and then posting the same name over HTTP (the row was absent from the list and the summary totals, and the create answered `500`). |

- **Example** (captured live):

  ```bash
  curl -s -i -X POST http://localhost:5000/v1/products \
    -H "Content-Type: application/json" \
    -d '{"name":"Docs-Widget-A","price":19.99}'
  # HTTP/1.1 201 Created
  # {"name":"Docs-Widget-A","price":19.99,"isDiscontinued":false,"supplierReferenceCode":null,
  #  "createdBy":"System","createdOn":"2026-10-26T04:59:24.068094+00:00",
  #  "id":"01a17693-dc06-7cf5-b296-8ec04941f254"}

  curl -s -i -X POST http://localhost:5000/v1/products \
    -H "Content-Type: application/json" \
    -d '{"name":"Docs-Widget-A","price":25}'
  # HTTP/1.1 409 Conflict
  # {"type":"Conflict","title":"Error","status":409,
  #  "errors":[{"message":"The product name 'Docs-Widget-A' is already taken.",
  #             "code":"precondition.product-name-taken","field":"Name"}],
  #  "traceId":"0HNPJ3KIFAEF9:00000001","code":"precondition.product-name-taken"}

  curl -s -i -X POST http://localhost:5000/v1/products \
    -H "Content-Type: application/json" \
    -d '{"name":"Docs-Widget-Neg","price":-5}'
  # HTTP/1.1 201 Created — the [Range] gap: a negative price still creates the product.
  ```

**Express a rule you need enforced as a validator, not as an attribute** — the lesson both this route's gaps and the two working preconditions below illustrate together.

### `GET /`

Generic list (`DKNet.AspCore.Extensions`'s `MapGetList`) over `ProductDto`, with the uniform `filter`/`search`/`orderBy`/`desc`/`pageNumber`/`pageSize` contract. `pageSize` defaults to `1000` with a configurable ceiling of `1000` (confirmed live: an empty query returns `"pageSize":1000`). `fromDate`/`toDate` last-activity bounds which, left out, window an audited listing to the last three months — `Product` is audited. Full contract: [Generic List Endpoint](../../generic-list-endpoint.md).

- **Auth:** `products.read`.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `filter` | string | no | operation syntax and allowed fields: [Generic List Endpoint](../../generic-list-endpoint.md) | query |
  | `search` | string | no | see [Generic List Endpoint](../../generic-list-endpoint.md) | query |
  | `orderBy` | string | no | must name a real `ProductDto` field mapped to a real column — `grossMargin` does not qualify (see below) | query |
  | `desc` | bool | no | reverses `orderBy` | query |
  | `pageNumber` | int | no | default `1` | query |
  | `pageSize` | int | no | default and ceiling `1000` | query |
  | `fromDate` / `toDate` | datetime | no | last-activity bounds; defaults to the last three months when both are omitted | query |

- **Response:** `200` + the paged envelope (`items`, `pageCount`, `pageNumber`, `pageSize`, `totalItemCount`, `hasNextPage`, `hasPreviousPage`).
- **Errors:** `400` when a filter/order field does not map to a real `ProductDto`/entity column — see the `orderBy=grossMargin` example below, and [the trap this generalises](../../generic-list-endpoint.md#trap-a-dto-field-must-map-to-a-real-column).
- **Example** (captured live, abridged to two items):

  ```bash
  curl -s -i "http://localhost:5000/v1/products?pageSize=2"
  # HTTP/1.1 200 OK
  # {"items":[{"name":"Docs-Widget-Neg","price":-5.00, ...},{"name":"Docs-Widget-A","price":19.99, ...}],
  #  "pageCount":1,"pageNumber":1,"pageSize":2,"totalItemCount":2,"hasNextPage":false,"hasPreviousPage":false}

  curl -s -i "http://localhost:5000/v1/products?orderBy=grossMargin"
  # HTTP/1.1 400 Bad Request
  # {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Bad Request","status":400,
  #  "detail":"Cannot sort by 'grossMargin': no such field on ProductDto.",
  #  "traceId":"0HNPJ3KIFAEFN:00000001"}
  ```

### `GET /{id}`

Generic `MapGetById`.

- **Auth:** `products.read`.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `id` | guid | yes | must resolve to an existing, caller-owned product | route |

- **Response:** `200` + `ProductDto`.
- **Errors:** `404` on an unknown id.
- **Example** (captured live):

  ```bash
  curl -s -i http://localhost:5000/v1/products/01a17693-dc06-7cf5-b296-8ec04941f254
  # HTTP/1.1 200 OK
  # {"name":"Docs-Widget-A","price":19.99,"isDiscontinued":false,"supplierReferenceCode":null,
  #  "createdBy":"System","createdOn":"2026-10-26T04:59:24.068094+00:00",
  #  "id":"01a17693-dc06-7cf5-b296-8ec04941f254"}

  curl -s -i http://localhost:5000/v1/products/00000000-0000-0000-0000-000000000000
  # HTTP/1.1 404 Not Found
  # (empty body)
  ```

### `PUT /{id}`

Changes `Price`. Generated from `Product.ChangePrice(decimal)`. Raises `ProductPriceUpdatedEvent` on save (see [Integration](integration.md)).

- **Auth:** `products.write`.
- **Idempotency:** not applicable — `PUT` here sets price to the submitted value, so a replay has the same effect as the first call.
- **Concurrency:** none — `Product` declares no concurrency token (see [Data model](data-model.md)); the later `SaveChangesAsync` call overwrites the earlier one with no conflict signal.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `id` | guid | yes | must resolve to an existing product | route |
  | `price` | decimal | yes | `[Range(0.01, double.MaxValue)]` declared, **never evaluated** | body |

- **Response:** `200` + `ProductDto`.
- **Errors:** `404` on an unknown id.
- **Example** (captured live):

  ```bash
  curl -s -i -X PUT http://localhost:5000/v1/products/01a17696-72c2-774c-bfc8-8bb5cdb25e86 \
    -H "Content-Type: application/json" -d '{"price":-1}'
  # HTTP/1.1 200 OK (abridged) — price:-1 accepted, the same [Range] gap as POST /.

  curl -s -i -X PUT http://localhost:5000/v1/products/00000000-0000-0000-0000-000000000000 \
    -H "Content-Type: application/json" -d '{"price":5}'
  # HTTP/1.1 404 Not Found
  # {"type":"NotFound","title":"Error","status":404,
  #  "errors":[{"message":"Product '00000000-0000-0000-0000-000000000000' was not found."}],
  #  "traceId":"0HNPJ3KIFAEGS:00000001"}
  ```

### `DELETE /{id}`

Generic `MapDeleteById`, bound to the generated `DeleteProductRequest`.

- **Auth:** `products.write`.
- **Idempotency:** a second delete of the same id answers `404`, not a repeated `204` (confirmed live) — a replay is not silently accepted as success.
- **Concurrency:** not applicable — delete has no body to conflict with a concurrent write.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `id` | guid | yes | must resolve to an existing, not-for-sale product | route |

- **Errors:**

  | Status | Code | When |
  |---|---|---|
  | `409` | `precondition.product-delete-while-for-sale` | `DeleteProductRequestValidator` finds the product still for sale (`IsDiscontinued == false`); discontinue it first. An unknown id passes this check on purpose, so the route's own `404` still fires for it rather than being hidden behind a `409`. |
  | `404` | — | Unknown id, or an id already deleted. |

- **Response:** `204 No Content` on success.
- **Example** (captured live):

  ```bash
  curl -s -i -X DELETE http://localhost:5000/v1/products/01a17694-2084-7947-b17d-c062e59fa912
  # HTTP/1.1 409 Conflict — product still for sale
  # {"type":"Conflict","title":"Error","status":409,
  #  "errors":[{"message":"The product is still for sale and cannot be deleted.",
  #             "code":"precondition.product-delete-while-for-sale","field":"Id"}],
  #  "traceId":"0HNPJ3KIFAEFL:00000001","code":"precondition.product-delete-while-for-sale"}

  curl -s -i -X DELETE http://localhost:5000/v1/products/01a17693-dc06-7cf5-b296-8ec04941f254
  # HTTP/1.1 204 No Content — a different product, already discontinued
  ```

### `POST /{id}/approval`

Domain action — `[CrudAction("approval")] Approve(string byUser)`. Stamps `UpdatedBy` with the caller-supplied `byUser`.

- **Auth:** `products.write`.
- **Idempotency:** repeating the same call re-stamps the same value — effectively idempotent in its result, though each call still updates `UpdatedOn`.
- **Concurrency:** none — same as `PUT /{id}`.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `id` | guid | yes | must resolve to an existing product | route |
  | `byUser` | string | yes | none — mirrors the method's parameter 1:1; a generated request can never carry `[FromClaim]`, so **any caller can name any approver** — this is caller-supplied, not derived from the authenticated identity | body |

- **Response:** `200` + `ProductDto`.
- **Errors:** `404` on an unknown id.
- **Example** (captured live):

  ```bash
  curl -s -i -X POST http://localhost:5000/v1/products/01a17696-9841-79f6-ac4e-150606e38228/approval \
    -H "Content-Type: application/json" -d '{"byUser":"doc-approver"}'
  # HTTP/1.1 200 OK (abridged)
  # {"name":"Docs-Widget-Approve","price":3.00, ..., "updatedBy":"doc-approver", ...}

  curl -s -i -X POST http://localhost:5000/v1/products/00000000-0000-0000-0000-000000000000/approval \
    -H "Content-Type: application/json" -d '{"byUser":"x"}'
  # HTTP/1.1 404 Not Found
  # {"type":"NotFound","title":"Error","status":404,
  #  "errors":[{"message":"Product '00000000-0000-0000-0000-000000000000' was not found."}],
  #  "traceId":"0HNPJ3KIFAEGT:00000001"}
  ```

### `PUT /{id}/supplier-reference`

Domain action overriding both segment and verb — `[CrudAction("supplier-reference", Verb = CrudActionVerb.Put)] AssignSupplierReference(string)`. Assigns the `[SensitiveData]` property after creation; it is not a create-request field.

- **Auth:** `products.supplier` — the only route on this scope, set per-route in `ProductV1Endpoint.Map` (`o.Configure("AssignSupplierReference", …)`) rather than by the group's `PUT` declaration, because holding `products.write` alone must not be enough to assign it.
- **Idempotency:** repeating the same call sets the same value — effectively idempotent in its result.
- **Concurrency:** none — same as `PUT /{id}`.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `id` | guid | yes | must resolve to an existing product | route |
  | `supplierReferenceCode` | string | yes | `[StringLength(50)]` declared, **never evaluated** on this route | body |

- **Response:** `200` + `ProductDto`.
- **Errors:**

  | Status | Code | When |
  |---|---|---|
  | `404` | — | Unknown id. |
  | `500` | — | A value over 50 characters reaches the `varchar(50)` column and fails there (confirmed live). The body observed was the service's ordinary structured `errors[]` shape with a generic message — unlike the discontinue route's collision below, it carried no stack trace. |

- **Example** (captured live):

  ```bash
  curl -s -i -X PUT http://localhost:5000/v1/products/01a17693-dc06-7cf5-b296-8ec04941f254/supplier-reference \
    -H "Content-Type: application/json" -d '{"supplierReferenceCode":"DOCS-REF-1"}'
  # HTTP/1.1 200 OK (abridged)
  # {"name":"Docs-Widget-A","price":29.99,"isDiscontinued":false,"supplierReferenceCode":"DOCS-REF-1", ...}

  curl -s -i -X PUT http://localhost:5000/v1/products/00000000-0000-0000-0000-000000000000/supplier-reference \
    -H "Content-Type: application/json" -d '{"supplierReferenceCode":"x"}'
  # HTTP/1.1 404 Not Found
  # {"type":"NotFound","title":"Error","status":404,
  #  "errors":[{"message":"Product '00000000-0000-0000-0000-000000000000' was not found."}],
  #  "traceId":"0HNPJ3KIFAEGU:00000001"}
  ```

### `PUT /{id}/discontinue`

Hand-written. Excluded from the generated map by name (`o.Exclude("Discontinue")`) because discontinuing a product also creates its named replacement in the same transaction, and an operation that writes more than one aggregate cannot be generated. Raises `ProductIsDiscontinuedUpdatedEvent` on save for the discontinued product, and `ProductCreatedEvent` for its replacement (see [Integration](integration.md)).

- **Auth:** `products.discontinue` — its own scope, named explicitly on this route (the group's `PUT` declaration cannot separate `ChangePrice`, `AssignSupplierReference` and this route from each other).
- **Idempotency:** **not** a repeatable no-op. A second call on an already-discontinued product is a domain failure — `400 Bad Request` (not `409`; confirmed live), raised by `DiscontinueProductCommandHandler` itself, not by a validator.
- **Concurrency:** none — two concurrent calls on the same id can both read `IsDiscontinued == false` before either saves, so both pass the already-discontinued check and both insert a replacement.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `id` | guid | yes | must resolve to an existing, not-yet-discontinued product | route |
  | `replacementName` | string | yes | length 1–150, enforced by `DiscontinueProductCommandValidator` | body |
  | `replacementPrice` | decimal | yes | `> 0`, enforced by `DiscontinueProductCommandValidator` | body |

  Confirmed live: an empty name and a zero price both return `400` with field-level messages.
- **Response:** `200` + `ProductDto` — the **discontinued** product, not the new replacement.
- **Errors:**

  | Status | Code | When |
  |---|---|---|
  | `404` | — | Unknown id. |
  | `400` | — | Empty/over-length `replacementName`, non-positive `replacementPrice`, or the product is already discontinued. |
  | `500` | — | `replacementName` collides with an existing product's unique index. Not caught by the validator — it reaches `SaveChangesAsync` as a raw `DbUpdateException`/Postgres `23505` and is returned with a full stack-trace body (confirmed live against this Development instance; whether a non-Development environment suppresses that detail is not traced here — see ❓ Open questions). The original product is left unchanged: both writes commit in the same `SaveChangesAsync` call, so the failed insert rolls back the `Discontinue()` call too (confirmed live by re-fetching the original product after the `500`). |

- **Example** (captured live):

  ```bash
  curl -s -i -X PUT http://localhost:5000/v1/products/01a17693-dc06-7cf5-b296-8ec04941f254/discontinue \
    -H "Content-Type: application/json" \
    -d '{"replacementName":"Docs-Widget-A (replacement)","replacementPrice":21.99}'
  # HTTP/1.1 200 OK
  # {"name":"Docs-Widget-A","price":29.99,"isDiscontinued":true,"supplierReferenceCode":"DOCS-REF-1",
  #  "createdBy":"System","createdOn":"2026-10-26T04:59:24.068094+00:00",
  #  "updatedBy":"System","updatedOn":"2026-10-26T04:59:34.839962+00:00",
  #  "id":"01a17693-dc06-7cf5-b296-8ec04941f254"}

  curl -s -i -X PUT http://localhost:5000/v1/products/01a17693-dc06-7cf5-b296-8ec04941f254/discontinue \
    -H "Content-Type: application/json" \
    -d '{"replacementName":"Docs-Widget-A (replacement 2)","replacementPrice":22.99}'
  # HTTP/1.1 400 Bad Request
  # {"type":"BadRequest","title":"Error","status":400,
  #  "errors":[{"message":"The product 01a17693-dc06-7cf5-b296-8ec04941f254 is already discontinued."}],
  #  "traceId":"0HNPJ3KIFAEFI:00000001"}

  curl -s -i -X PUT http://localhost:5000/v1/products/00000000-0000-0000-0000-000000000000/discontinue \
    -H "Content-Type: application/json" -d '{"replacementName":"x","replacementPrice":1}'
  # HTTP/1.1 404 Not Found
  # {"type":"NotFound","title":"Error","status":404,
  #  "errors":[{"message":"The product 00000000-0000-0000-0000-000000000000 was not found."}],
  #  "traceId":"0HNPJ3KIFAEGV:00000001"}
  ```

![A caller discontinues a product; the handler loads it, discontinues it and inserts its replacement in one SaveChangesAsync call, with the already-discontinued and unknown-id refusals short-circuiting before any write.](diagrams/discontinue.svg)

This diagram cannot show the `500` a colliding replacement name produces — that path exists (confirmed live) but has no handler-level `catch`, so it is not a modeled branch of this request. See [Domain actions with `[CrudAction]`](../../crud-attributes.md#domain-actions-with-crudaction) for how this route would have looked had the generator been able to express it.

### `GET /summary`

Hand-written — no generated shape exists for an aggregate query. `{ productCount, averagePrice }` across every product the caller can see (discontinued included) — the handler's own doc comment says "every product the caller can see," which, because `Product` implements `IOwnedBy` and the global ownership filter scopes every read to the caller's own rows, means every product that caller owns.

- **Auth:** `products.read` (no per-route scope call needed — it is a `GET`, covered by the group's declaration).
- **Response:** `200` + `{ "productCount": int, "averagePrice": decimal }`.
- **Example** (captured live, verbatim — note the decimal's full scale):

  ```bash
  curl -s http://localhost:5000/v1/products/summary
  # {"productCount":3,"averagePrice":15.6600000000000000}
  ```

### Sensitive data: what each caller sees

`Product` declares `[SensitiveData("pricing")] SupplierCostPrice` and `[SensitiveData] SupplierReferenceCode`; `ProductDto.GrossMargin` carries `[SensitiveData("pricing")]` too (see below). `Minimal.Api` opts the response serializer into role-aware filtering once at start-up — `.AddSensitiveDataPrincipalAccessor()` registers `ISensitiveDataPrincipalAccessor`, and `op.SerializerOptions.UseRoleAwareSensitiveData(...)` in `ServiceConfigs` wires it into JSON serialization — a caller in the `pricing` role gets `supplierCostPrice`/`grossMargin`; any other authenticated caller does not; an unauthenticated caller gets none of the three properties. *Absent* means the key is missing from the JSON object entirely, not `null` and not masked. Full decision rules: [`api-pipeline.md`](../../api-pipeline.md#role-aware-sensitive-property-filtering); declaring one of your own: [`crud-attributes.md`](../../crud-attributes.md#declaring-a-sensitive-property).

| Caller | `supplierReferenceCode` | `supplierCostPrice` / `grossMargin` |
|---|---|---|
| Unauthenticated | absent | absent |
| Authenticated, without the `pricing` role — **what this template's own Development run gives you**, since its built-in demonstration authenticator issues every caller an identity with no role claims | present | absent |
| Authenticated, in the `pricing` role | present | present (`grossMargin` holds an explicit JSON `null` if `supplierCostPrice` was never disclosed) |

Confirmed by `ProductSensitiveDataDemoAuthenticatedTests` (row 2) and `ProductGrossMarginTests` (row 3, including the explicit-`null` case). The opt-in is one serializer setting for the whole service, but it only acts on declared properties — the hand-written purchase-order sample declares nothing sensitive, so its responses are byte-for-byte unchanged by it. Reaching row 3 outside a test means wiring a real identity provider, which this template deliberately ships only as a placeholder — see [`template-usage.md`](../../template-usage.md#what-you-must-change-before-shipping).

### A response value the convention cannot produce

`ProductDto` carries one property not mirrored from a `Product` column: **`GrossMargin`** (`Price` minus `SupplierCostPrice`). The DTO generator's name-matching convention only mirrors an entity property under the same name, so a value derived from two columns is outside what it can emit. Three pieces put it on every response, and none of them is a route:

| Piece | What it does |
|---|---|
| The property | `[SensitiveData("pricing")] decimal? GrossMargin`, hand-written into the generated `partial record ProductDto`. |
| The mapping | A Mapster `IRegister` (`ProductMappingRegister`): `config.ForType<Product, ProductDto>().Map(d => d.GrossMargin, s => s.Price - s.SupplierCostPrice)`. `ForType` merges onto the convention's existing config; `NewConfig` would have replaced it, taking every convention-mapped property with it. |
| The wiring | `MapsToExtensions` scans the assembly for every `IRegister` after the convention loop runs, so the merge survives with no per-DTO wiring. |

Every other value on the response still comes from the convention untouched; only this one property is hand-mapped. Two structure tests pin that no route, request or handler was written or dropped for it: `ProductV1Endpoint` contains no reference to `GrossMargin`, and no `*GrossMargin*Request`/`*Handler` type exists anywhere in the assembly (`ProductGrossMarginStructureTests`). **Needing a response the convention cannot produce is not a reason to hand-write the route.**

Ordering or filtering by `grossMargin` answers `400` by construction, not by luck: the query surface requires the field on the stored record as well as the response, and `GrossMargin` exists only on the response (confirmed live, see `GET /` above). The rest of the query surface is unaffected — `?filter=price:GreaterThan:100` and `?search=` behave as usual (`ProductGrossMarginTests`), with the margin reported on each surviving item **for a caller in the `pricing` role**; any other caller still gets no `grossMargin` key at all on those same items.

To add a derived value of your own: write the property on the DTO's `partial record`, add an `IRegister` beside it that `ForType`-maps it, and stop — the scan wires it. Keep the mapping a plain property expression so it stays EF-translatable where the list route projects it over `IQueryable<Product>`.

## ⚠️ Errors & limits

Every error on this feature's routes uses the service's standard problem body — `traceId` plus, for a precondition, a machine-readable `code` starting with `precondition.` (the one signal the shared `FluentValidationConfig` error-response setting uses to tell a refused precondition `409` from ordinary refused input `400`). Three shapes appear: a `404` with an empty body on `GET /{id}` and `DELETE /{id}`, but a structured `errors[]` body with a message on the other write routes' `404`s; `400`/`409` problem+json with `errors[]` and (for preconditions) `code`; and the list route's `400` in RFC 9110 shape with a plain `detail` string. A `201` response carries `Location: /` rather than the created resource's URL.

Paging: `pageSize` defaults to and caps at `1000`; see [Generic List Endpoint](../../generic-list-endpoint.md) for the full contract. Rate limiting and ordering/consistency are the whole application's concerns, not this feature's — see [Configuration](../../configuration.md).

**What this feature deliberately does not do:**

- No request idempotency on `POST /` — if a `201` response is lost, retrying gets `409` (the name is now taken), not the original `201`; the caller has to `GET` by name to learn the first call succeeded.
- No `[Range]`/`[StringLength]`/`[Required]` enforcement on a generated route — every attribute above marked "declared, never evaluated" reaches the database or the response unchecked: an empty name creates successfully, a negative price creates and updates successfully, and a value past a column's length crashes with `500` instead of refusing with `400`.
- No filtered get-by-id and no custom list predicate beyond the generic contract — see [CRUD Attributes](../../crud-attributes.md#the-four-attributes).
- No `[FromClaim]` acting-user population anywhere on this slice — a generated request can never carry it, so `Approve`'s `byUser` is caller-supplied, not derived from the authenticated identity.
