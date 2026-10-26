# Purchase Orders — Endpoints

Part of [Purchase Orders](index.md).

Base path: `/v1/purchase-orders`. Every route is a literal `MapPost`/`MapGet`/`MapPut`/`MapDelete`
call in `PurchaseOrderV1Endpoint` — none of it goes through the generic CRUD route-registration
helper (see [Automated Products](../automated-products/index.md) for that style).

## 🔌 Endpoints

| Verb | Path | Purpose | Auth |
|---|---|---|---|
| `POST` | `/` | Create a purchase order | authenticated caller with a resolvable acting user and ownership key |
| `GET` | `/` | List purchase orders, paged, optionally filtered by customer name | authenticated caller; sees only its own orders |
| `GET` | `/{id}` | Get one purchase order by id | authenticated caller; its own orders only |
| `PUT` | `/{id}` | Change an order's amount | authenticated caller; its own orders only |
| `POST` | `/{id}/cancel` | Cancel an order | authenticated caller; its own orders only |
| `DELETE` | `/{id}` | Delete an order | authenticated caller; its own orders only |

Whether an anonymous call is rejected before it reaches any of the above depends on
`FeatureManagement:RequireAuthorization` — off in Development, on elsewhere; see
[API Request Pipeline](../../api-pipeline.md). Every write route resolves the acting user from the
caller's `ClaimTypes.Name` claim via `[FromClaim]` and `AddContextualRequestPopulation`; a payload
`byUser` field is never read for this.

### `POST /`

Creates a purchase order for a customer and amount.

- **Auth:** authenticated caller; the handler refuses (`400`) a caller whose `ClaimTypes.Name` claim
  did not resolve, and ownership enforcement refuses (`403`) one with no ownership key at all.
- **Idempotency:** required. Send `X-Idempotency-Key: {Guid}`. A new key runs the create and caches
  the response; a replayed key is refused with `409 Conflict` — this deployment does **not** replay
  the original `201` body (`ConflictHandling = ConflictResponse` in `Minimal.Api/Configs/AppConfig.cs`).
  A request with no key, or an invalid one, is refused with `400` before anything else runs.
- **Concurrency:** not applicable — this route only inserts a new row.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `customerName` | string | yes | 1–200 characters | body |
  | `amount` | decimal | yes | greater than 0 | body |
  | `X-Idempotency-Key` | GUID | yes | — | header |

- **Response:** `201 Created`

  ```json
  {"id":"8daca0f3-a90e-4501-91b5-21cbcb76c8bb","customerName":"Docs Test Co","amount":99.90,"status":"placed","createdBy":"demo-user@not-a-real-identity.invalid"}
  ```

- **Errors:**

  | Status | Code | When |
  |---|---|---|
  | `400` | `NotEmptyValidator` / `LengthValidator` / `GreaterThanValidator` | `customerName` empty/too long, or `amount` not greater than 0 |
  | `400` | — | `X-Idempotency-Key` header missing or invalid |
  | `400` | — | the acting-user claim did not resolve |
  | `403` | — | authenticated, but no ownership key resolved; no row is persisted |
  | `409` | — | the same `X-Idempotency-Key` was already processed |

- **Example:**

  ```bash
  curl -i -X POST http://localhost:5077/v1/purchase-orders \
    -H "Content-Type: application/json" \
    -H "X-Idempotency-Key: $(uuidgen)" \
    -d '{"customerName":"Acme Pte Ltd","amount":250.00}'
  ```

  Replaying the same command with the same `X-Idempotency-Key` value:

  ```json
  {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.10","title":"Conflict","status":409,"detail":"The request with the same idempotent key `4581D150-D72F-4384-AE88-D6E0341B743D` has already been processed.","traceId":"0HNPJ3KIFAEFQ:00000001"}
  ```

  A validation failure (empty name, zero amount):

  ```json
  {"type":"BadRequest","title":"Error","status":400,"errors":[{"message":"'Customer Name' must not be empty.","code":"NotEmptyValidator","field":"CustomerName"},{"message":"'Customer Name' must be between 1 and 200 characters. You entered 0 characters.","code":"LengthValidator","field":"CustomerName"},{"message":"'Amount' must be greater than '0'.","code":"GreaterThanValidator","field":"Amount"}],"traceId":"0HNPJ3KIFAEFS:00000001","code":"NotEmptyValidator"}
  ```

### `GET /`

Lists purchase orders, paged and optionally filtered to one customer. Returns only the caller's own
orders.

- **Auth:** authenticated caller.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `pageIndex` | int | no | `> 0`; defaults to `1` when omitted | query |
  | `pageSize` | int | no | `1`–`100`; defaults to `20` when omitted | query |
  | `customerName` | string | no | exact match, case-sensitive — not a "contains" search | query |

- **Response:** `200 OK` — a bare JSON array of orders, **with no paging envelope** (no total count,
  no "has next page"):

  ```json
  [{"id":"6e6f4d3c-1b7e-4c7a-9f1d-8a2b5c6d7e01","customerName":"Acme Pte Ltd","amount":1250.00,"status":"placed","createdBy":"System"},{"id":"6e6f4d3c-1b7e-4c7a-9f1d-8a2b5c6d7e02","customerName":"Globex Corporation","amount":875.50,"status":"placed","createdBy":"System"},{"id":"6e6f4d3c-1b7e-4c7a-9f1d-8a2b5c6d7e03","customerName":"Initech LLC","amount":430.25,"status":"placed","createdBy":"System"}]
  ```

  On a fresh database this is exactly the three rows `PurchaseOrderStaticData` seeds (see
  [Data model](data-model.md)).

- **Errors:**

  | Status | Code | When |
  |---|---|---|
  | `400` | `InclusiveBetweenValidator` | `pageSize` outside `1`–`100` |
  | `400` | `GreaterThanValidator` | `pageIndex` `<= 0` |

- **Example:**

  ```bash
  curl -i "http://localhost:5077/v1/purchase-orders?CustomerName=Acme%20Pte%20Ltd&PageIndex=1&PageSize=2"
  ```

  ```json
  [{"id":"6e6f4d3c-1b7e-4c7a-9f1d-8a2b5c6d7e01","customerName":"Acme Pte Ltd","amount":1250.00,"status":"placed","createdBy":"System"}]
  ```

### `GET /{id}`

Gets one purchase order by id.

- **Auth:** authenticated caller; another caller's order answers exactly like an unknown id.
- **Response:** `200 OK` with the order, or `404 Not Found` with an empty body.
- **Errors:**

  | Status | Code | When |
  |---|---|---|
  | `404` | — | the id does not exist, or belongs to another caller |

- **Example:**

  ```bash
  curl -i http://localhost:5077/v1/purchase-orders/00000000-0000-0000-0000-000000000000
  ```

  ```http
  HTTP/1.1 404 Not Found
  Content-Length: 0
  ```

### `PUT /{id}`

Changes an order's amount. No other field can be changed through this route.

- **Auth:** authenticated caller; its own orders only.
- **Idempotency:** no idempotency key on this route; naturally safe to retry — re-sending the same
  `amount` produces the same state each time.
- **Concurrency:** none — the entity carries no row version or `ETag`. The last `PUT` to reach
  `SaveChanges` wins silently; an earlier concurrent write is overwritten with no conflict signal.
- **Request:**

  | Field | Type | Required | Rules | From |
  |---|---|---|---|---|
  | `id` | GUID | yes | must exist and belong to the caller | route |
  | `amount` | decimal | yes | greater than 0 | body |

- **Response:** `200 OK` with the updated order.
- **Errors:**

  | Status | Code | When |
  |---|---|---|
  | `400` | `GreaterThanValidator` | `amount` not greater than 0 |
  | `400` | — | the acting-user claim did not resolve |
  | `404` | — | the id does not exist, or belongs to another caller |

- **Example:**

  ```bash
  curl -i -X PUT http://localhost:5077/v1/purchase-orders/6e6f4d3c-1b7e-4c7a-9f1d-8a2b5c6d7e02 \
    -H "Content-Type: application/json" -d '{"amount":999.99}'
  ```

  ```json
  {"id":"6e6f4d3c-1b7e-4c7a-9f1d-8a2b5c6d7e02","customerName":"Globex Corporation","amount":999.99,"status":"placed","createdBy":"System"}
  ```

### `POST /{id}/cancel`

Cancels an order. Refuses a second cancel rather than silently repeating it.

- **Auth:** authenticated caller; its own orders only.
- **Idempotency:** no idempotency key on this route; a retry after success gets `409` instead of
  repeating the cancellation — safe, but surfaced as an error rather than a replayed `200`.
- **Response:** `200 OK` with the cancelled order.
- **Errors:**

  | Status | Code | When |
  |---|---|---|
  | `400` | — | the acting-user claim did not resolve |
  | `404` | — | the id does not exist, or belongs to another caller |
  | `409` | `precondition.purchase-order-already-cancelled` | the order is already cancelled |

- **Example:**

  ```bash
  curl -i -X POST http://localhost:5077/v1/purchase-orders/8daca0f3-a90e-4501-91b5-21cbcb76c8bb/cancel
  ```

  First call:

  ```json
  {"id":"8daca0f3-a90e-4501-91b5-21cbcb76c8bb","customerName":"Docs Test Co","amount":99.90,"status":"cancelled","createdBy":"demo-user@not-a-real-identity.invalid"}
  ```

  Second call, same id:

  ```json
  {"type":"Conflict","title":"Error","status":409,"errors":[{"message":"The purchase order 8daca0f3-a90e-4501-91b5-21cbcb76c8bb is already cancelled.","code":"precondition.purchase-order-already-cancelled"}],"traceId":"0HNPJ3KIFAEG5:00000001","code":"precondition.purchase-order-already-cancelled"}
  ```

### `DELETE /{id}`

Deletes an order outright — there is no soft delete.

- **Auth:** authenticated caller; its own orders only.
- **Idempotency:** no idempotency key on this route; a retry after success gets `404` instead of
  repeating the deletion — safe, but surfaced as an error rather than a replayed `200`.
- **Response:** `200 OK` with an empty body.
- **Errors:**

  | Status | Code | When |
  |---|---|---|
  | `400` | — | the acting-user claim did not resolve |
  | `404` | — | the id does not exist, or belongs to another caller |

- **Example:**

  ```bash
  curl -i -X DELETE http://localhost:5077/v1/purchase-orders/00000000-0000-0000-0000-000000000000
  ```

  ```json
  {"type":"NotFound","title":"Error","status":404,"errors":[{"message":"The purchase order 00000000-0000-0000-0000-000000000000 was not found."}],"traceId":"0HNPJ3KIFAEG7:00000001"}
  ```

## ⚠️ Errors & limits

Two different error body shapes appear on this feature, depending on which layer refuses the
request:

- A refusal raised by a command handler or FluentValidation (bad input, not found, already
  cancelled, the acting-user guard) answers `{"type","title","status","errors":[{"message","code"?,
  "field"?}],"traceId","code"?}` — `code` is set only when a rule carries one (FluentValidation's
  validator name, or a `precondition.*` code), and a `precondition.*` code is what maps the response
  to `409` instead of the rule's ordinary status.
- A refusal from the idempotency filter itself (missing/invalid key, or a replayed key) answers the
  plain RFC 9110 problem shape instead: `{"type","title","status","detail","traceId"}` — no `errors`
  array, no `code`.

**Paging:** `pageIndex` defaults to `1`, `pageSize` to `20`, capped at `100`; both are validated, not
clamped — an out-of-range value is a `400`, not a silently adjusted page. The list response is a bare
array with no total count or "has more" flag; a caller that needs to know when to stop paging has to
keep requesting pages until one comes back shorter than `pageSize`.

**Filtering:** `customerName` is an exact, case-sensitive match (`SpecGetPurchaseOrder`), not a
"contains" search.

**Ordering:** the list query applies no explicit sort — row order is whatever the database returns,
not guaranteed stable from one page to the next.

**Consistency:** every route reads and writes the same PostgreSQL database directly; there is no
cache and nothing eventually consistent here. The one asynchronous step is the domain event delivery
to `PurchaseOrderCreatedEventHandler`, which only logs and cannot be observed through the HTTP API.

**Retry safety:** `POST /` is the only route that needs — and gets — idempotency protection.
`PUT /{id}` is naturally safe to retry (re-applying the same amount is harmless); `POST
/{id}/cancel` and `DELETE /{id}` are safe to retry because a second call answers `409`/`404` instead
of repeating the effect.

**Rate limiting, CORS, authentication and versioning** are handled once for the whole application —
see [API Request Pipeline](../../api-pipeline.md).

**This feature deliberately does not:** soft-delete (delete is a hard `DELETE FROM`), publish its
event to an external broker (see [Integration](integration.md)), or expose a partial update (`PUT`
only ever changes `amount`).
