# Automated Products — Data model

Part of [Automated Products](index.md).

## 🗃️ Data model

### Product — `sample.Products`

One row is one sellable product: its name, price, discontinued flag, and the two supplier fields a non-`pricing` caller never sees.

| Field | Column | DB type | Length / precision | Required | Key / index | Default | Purpose |
|---|---|---|---|---|---|---|---|
| `Id` | `Id` | `uuid` | — | yes | `PK` | — | Identity assigned at create (`AggregateRoot`). |
| `Name` | `Name` | `varchar` | 150 | yes | `unique` | — | Catalogue name; `CreateProductRequestValidator` turns a collision into `409` before the database's own unique index would reject the insert. |
| `Price` | `Price` | `numeric` | `(18,2)` | yes | — | — | Current unit price; `[Range(0.01, double.MaxValue)]` is declared on the generated request but never enforced — see [Errors & limits](endpoints.md#-errors--limits). |
| `IsDiscontinued` | `IsDiscontinued` | `boolean` | — | yes | — | — | Drives the delete precondition and the lifecycle below. `\d+ sample."Products"` shows no database-level default; every `Product` is constructed with it `false`. |
| `OwnedBy` | `OwnedBy` | `varchar` | 500 | yes | — | — | Row-level ownership key `DataOwnerHook` stamps on insert; excluded from `ProductDto` because `CreatedBy` already exposes the same value. |
| `CreatedBy` | `CreatedBy` | `varchar` | 255 | yes | — | — | Audit-hook acting user at insert. |
| `CreatedOn` | `CreatedOn` | `timestamptz` | — | yes | — | — | Audit-hook insert timestamp. |
| `UpdatedBy` | `UpdatedBy` | `varchar` | 255 | no | — | — | Audit-hook acting user at the last modify. |
| `UpdatedOn` | `UpdatedOn` | `timestamptz` | — | no | — | — | Audit-hook last-modify timestamp. |
| `SupplierCostPrice` | `SupplierCostPrice` | `numeric` | `(18,2)` | no | — | — | Confidential supplier cost; `[SensitiveData("pricing")]` — present only for a caller in the `pricing` role. |
| `SupplierReferenceCode` | `SupplierReferenceCode` | `varchar` | 50 | no | — | — | Supplier's reference; `[SensitiveData]` — present for any authenticated caller, absent for an unauthenticated one. `[StringLength(50)]` on the assign-action request is forwarded but not enforced: a value longer than the column surfaces as a `500` from the database, confirmed live. |

No foreign keys: `Product` has no relation to another entity in this slice. `GrossMargin` (`Price − SupplierCostPrice`) is a response-only value with no column of its own — `ProductMappingRegister` hand-maps it onto `ProductDto` — so it cannot be filtered or ordered on over HTTP (confirmed live, `GET /v1/products?orderBy=grossMargin` → `400`). See [the GrossMargin walkthrough](endpoints.md#a-response-value-the-convention-cannot-produce) for how it is added without a route.

**Concurrency.** `ProductConfigs` declares no concurrency token (no `IsConcurrencyToken`, no `IsRowVersion` property, confirmed by grepping `CoreDbContextModelSnapshot.cs`), and the Postgres system column `xmin` is never surfaced to EF Core here — `\d+` cannot show it either way, since it is a system column, not a mapped one. None: last write wins.

**Seed data.** No migration-time static seed writes a `Product` row — unlike the manual sample's `PurchaseOrderStaticData`, no `DataSeedingConfiguration<Product>` exists in this repository, confirmed by `GET /v1/products?filter=Name:Equal:Demo-Product-With-Supplier-Data` returning zero items against the API's own self-migrated database. The one seeded-looking row a reader may see locally — `Demo-Product-With-Supplier-Data`, `supplierCostPrice` `42.50`, `supplierReferenceCode` `DEMO-SUPPLIER-REF` — comes from the Aspire app host's `SampleDataGenerator`, only when `SampleData:RecordsPerEntity` is above zero; it is not a counter-example to "no static seed."

**Status.**

| Value | Meaning | Reached by | Next |
|---|---|---|---|
| `IsDiscontinued = false` | For sale | Create | `PUT /{id}/discontinue` |
| `IsDiscontinued = true` | Discontinued | `PUT /{id}/discontinue` | `DELETE /{id}` |

The lifecycle diagram lives in [index.md](index.md#-end-to-end-flow).
