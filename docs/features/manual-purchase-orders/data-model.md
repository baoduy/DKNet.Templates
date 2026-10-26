# Purchase Orders — Data model

Part of [Purchase Orders](index.md).

## 🗃️ Data model

### PurchaseOrder — `manual_sample.PurchaseOrders`

One row is a single customer purchase order and its current lifecycle status.

| Field | Column | DB type | Length / precision | Required | Key / index | Default | Purpose |
|---|---|---|---|---|---|---|---|
| `Id` | `Id` | `uuid` | — | yes | `PK` | sequential GUID (`GuidV7ValueGenerator`), generated on add | identifies the order |
| `CustomerName` | `CustomerName` | `character varying` | 200 | yes | indexed (`IX_PurchaseOrders_CustomerName`) | — | the customer the order is for; also the field `GET /` filters on |
| `Amount` | `Amount` | `numeric` | 18,2 | yes | — | — | the order's monetary amount; the only field `PUT` can change |
| `Status` | `Status` | `text` | — | yes | — | — | lifecycle state (`Placed`/`Cancelled`); stored as its enum member name, not a number |
| `CreatedBy` | `CreatedBy` | `character varying` | 255 | yes | — | — | acting user who created the order, from the caller's `ClaimTypes.Name` claim |
| `CreatedOn` | `CreatedOn` | `timestamp with time zone` | — | yes | — | — | when the order was created |
| `UpdatedBy` | `UpdatedBy` | `character varying` | 255 | no | — | — | acting user of the last amount change or cancel; `null` until either happens |
| `UpdatedOn` | `UpdatedOn` | `timestamp with time zone` | — | no | — | — | when the order was last changed |
| `OwnedBy` | `OwnedBy` | `character varying` | 500 | yes | — | — | the tenant/caller ownership key that scopes row-level read and write access; stamped by `DataOwnerHook`, independent of `CreatedBy` — see [Row-level ownership filtering](../../auditing-and-data-ownership.md#row-level-ownership-filtering) |

No foreign keys: `PurchaseOrder` is a single-table aggregate with no child entities.

**Concurrency:** none. `PurchaseOrder` does not implement `IConcurrencyEntity<T>`, so EF Core's
`DefaultEntityTypeConfiguration` configures no row-version column for this table, confirmed against
the live schema (no `xmin`-mapped or `rowversion` column on `manual_sample.PurchaseOrders`). Two
concurrent `PUT`s to the same order are not detected — the later `SaveChanges` silently overwrites the
earlier one.

**Status:**

| Value | Meaning | Reached by | Next |
|---|---|---|---|
| `Placed` | Initial and default state — every order is created here | both `PurchaseOrder` constructors | `Cancelled` |
| `Cancelled` | Terminal | `Cancel(userId)`, from `Placed` only | none — terminal |

`PurchaseOrderStatus` also declares `Draft`, but no constructor, handler or migration in this sample
ever assigns it — every traced assignment to `Status` sets either `Placed` or `Cancelled`.

**Seed data:** `PurchaseOrderStaticData` seeds three fixed-id rows — `Acme Pte Ltd` (1250.00),
`Globex Corporation` (875.50), `Initech LLC` (430.25) — all `Placed`, all owned by
`SharedConsts.SystemAccount`. It is discovered by `UseAutoDataSeeding`, wired into both the DI-host
path (`InfraSetup.AddInfraServices`) and the startup-migration path (`InfraMigration.MigrateDb`), so
the three rows exist over `GET /v1/purchase-orders` on any fresh database, including this running
instance's.
