# DKNet API Solution Template

[![NuGet](https://img.shields.io/nuget/v/DKNet.Minimal.Template.svg)](https://www.nuget.org/packages/DKNet.Minimal.Template)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

`dotnet new` solution template for an ASP.NET Core microservice built as vertical slices over
DDD/CQRS — for a team starting a new .NET 10 service that wants the layering, the hardening and the
CRUD plumbing decided before the first feature is written.

## 📖 Overview

A scaffolded solution is a running HTTP API that ships with two worked features, each the same shape
of slice built a different way:

- [Products](docs/features/automated-products/index.md) — create, price, approve, discontinue and list
  the products in a caller's own catalogue; most of it is declared by attribute and generated.
- [Purchase Orders](docs/features/manual-purchase-orders/index.md) — create, correct, cancel and look
  up a customer's purchase orders, with a safe-to-retry create call; every layer is written by hand.

The API is called over HTTP by front-ends and other services, directly or through the typed
`Minimal.Client` package the solution also scaffolds. Which sample to copy for a feature of your own:
[Manual vs. Automated](docs/samples/manual-vs-automated.md#at-a-glance-which-one-should-i-copy).

## 🏗️ Runtime architecture

![A caller's HTTPS request crosses the Minimal.Api edge (security headers, CORS, authentication against your identity provider), reaches an IEndpointConfig route group, is sent over the in-memory IMessageBus to a Minimal.AppServices handler that calls a Minimal.Domains aggregate and saves through CoreDbContext to PostgreSQL, with idempotency keys in Redis and domain events forwarded to Azure Service Bus when it is configured.](docs/diagrams/runtime.svg)

Everything inside the trust boundary runs in the one `Minimal.Api` process. PostgreSQL is required:
the start-up migration throws without `ConnectionStrings:AppDb`. Redis is optional: without
`ConnectionStrings:Redis` the cache and the idempotency store fall back to in-process stores, correct
for a single instance only. Azure Service Bus is used only with `FeatureManagement:EnableServiceBus`
and `ConnectionStrings:AzureBus` both set. With `FeatureManagement:RequireAuthorization` on, tokens are
validated against your OIDC tenant; with it off, a built-in demonstration identity is attached to every
request. On local runs `Minimal.AppHost` (.NET Aspire) provisions the Redis and PostgreSQL containers
and injects both connection strings.

## 🌐 Downstream systems

| System | Direction | How | What for | When it is down |
|---|---|---|---|---|
| PostgreSQL | we call it | Npgsql through `CoreDbContext` | every stored aggregate, the start-up migration, the static seed rows | Without a connection string the host does not start. At run time Npgsql's `EnableRetryOnFailure()` defaults retry a transient failure before the request fails; `/healthz` carries a `CoreDbContext` check. Locally, with the database container stopped, `/healthz` answered `504 Gateway Timeout`. |
| Redis | we call it | StackExchange.Redis | distributed cache and idempotency keys | Not set: in-process stores, single instance only. Set but unreachable: see ❓ Open questions. |
| Identity provider (OIDC) | we call it | JWT bearer metadata fetch, only with `RequireAuthorization` on | signing keys, issuer and audience for token validation | See ❓ Open questions. |
| Azure Service Bus | it consumes our events | SlimMessageBus `AzureBus` child bus, topic `product-tp`, subscription `product-sub` | forwards `ProductCreatedEvent` | Off unless `EnableServiceBus` and `ConnectionStrings:AzureBus` are both set; the in-memory bus keeps working either way. |
| Azure App Configuration | we call it | `DefaultAzureCredential`, at start-up, only with `EnableAzureAppConfig` on | configuration and feature flags | See ❓ Open questions. |
| OTLP collector / Azure Monitor | we call it | OpenTelemetry exporters, only with `EnableOpenTelemetry` on | traces, metrics, logs | See ❓ Open questions. |

The keys that point at each system: [Configuration](docs/configuration.md#connectionstrings).

## 🚀 Quick Start

You need the .NET 10 SDK and a running Docker engine (the Aspire host starts Redis and PostgreSQL in
containers).

From any directory, add the GitHub Packages feed and install the template — the feed needs a GitHub
personal access token with `read:packages`:

```bash
dotnet nuget add source --username <GITHUB_USERNAME> --password <GITHUB_PAT_WITH_READ_PACKAGES> \
  --store-password-in-clear-text --name github "https://nuget.pkg.github.com/baoduy/index.json"
dotnet new install DKNet.Minimal.Template --nuget-source "https://nuget.pkg.github.com/baoduy/index.json"
```

Scaffold a solution (a dotted name works: `-n DKNet.Accounts`), then start the whole stack from the
new solution's root:

```bash
dotnet new dknet-minimal -n MyCompany.MyService
cd MyCompany.MyService
dotnet run --project ApiEndpoints/MyCompany.MyService.AppHost
```

The host prints the Aspire dashboard address, waits for both containers, starts the API on
`http://localhost:5000`, and fills the database with generated sample rows. Then make one call:

```bash
curl -s "http://127.0.0.1:5000/v1/products?pageSize=1"
```

```json
{"items":[{"name":"Handcrafted Granite Hat 55rgks","price":6342.90,"isDiscontinued":false,"supplierReferenceCode":null,"createdBy":"System","createdOn":"2026-10-26T05:48:51.74292+00:00","id":"01a176c1-269e-7fdd-ae0d-0158fd30fe95"}],"pageCount":10000,"pageNumber":1,"pageSize":1,"totalItemCount":10000,"hasNextPage":true,"hasPreviousPage":false}
```

The product name and ids are generated, so yours differ. To work on the template itself, the same
host runs from a fresh clone of this repository: `dotnet run --project src/ApiEndpoints/Minimal.AppHost`
from the repository root. Every scaffold parameter, and the run, test, migrate and publish commands:
[Template usage](docs/template-usage.md).

## ✅ Verify it works

With the host from 🚀 Quick Start running:

```bash
curl -s -i http://127.0.0.1:5000/healthz
```

```text
HTTP/1.1 200 OK
...
{"status":"Healthy"}
```

The public probe reports status only. The per-check report is at `/healthz/detail`, behind
authorization when `RequireAuthorization` is on.

## 🛠️ Common commands

From the repository root (`src/` holds the template solution):

| What | Command |
|---|---|
| Restore | `dotnet restore src/DKNet.Templates.sln` |
| Build | `dotnet build src/DKNet.Templates.sln -c Release` |
| Test, with coverage | `dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings --collect:"XPlat Code Coverage"` |
| Template guards (packaging, scaffolding, repo hygiene) | `dotnet test DKNet.Templates.slnx` |
| Run the full stack | `dotnet run --project src/ApiEndpoints/Minimal.AppHost` |
| Run the API alone | `ConnectionStrings__AppDb="Host=localhost;Database=AppDb;Username=postgres;Password=<password>" dotnet run --project src/ApiEndpoints/Minimal.Api` |
| Add or remove an EF Core migration | `./add-migration.sh <Name>` / `./remove-migration.sh <Name>`, from `src/ApiEndpoints/` |
| Pack the template | `dotnet pack DKNet.Minimal.Template.csproj -c Release -o ./nupkgs`, from `src/` |
| Validate the AI plugin | `./validate-plugin.sh` |

## ⚠️ Known limitations

- **The API alone needs a database.** `dotnet run --project …/Minimal.Api` without the Aspire host
  stops at start-up with `Startup database migration failed` (`The ConnectionString property has not
  been initialized`): the `Development` overlay runs the migration on start and ships
  `ConnectionStrings:AppDb` empty. Supply the connection string, as in 🛠️ Common commands.
- **`--TenantId` and `--ApiAudience` ship as placeholders, not working values.** Left as shipped, the
  OIDC metadata fetch runs against a tenant that does not exist and every token is rejected on audience
  mismatch. Replace them before you turn on `FeatureManagement:RequireAuthorization` — see
  [what you must change before shipping](docs/template-usage.md#what-you-must-change-before-shipping).
- **A stock Development run enforces no scopes.** `RequireAuthorization` is off and the demonstration
  identity is on, so every route answers without a token.
- **The generated Products routes do not enforce DataAnnotations and are not idempotent.** A negative
  price still creates a product, and a replayed create is a second create; see
  [Products endpoints](docs/features/automated-products/endpoints.md).
- **On macOS, port 5000 is also taken by the AirPlay Receiver.** If `http://localhost:5000` answers
  `403 Forbidden`, call `http://127.0.0.1:5000`, as the examples above do.

## 📚 Documentation

| Page | What it answers |
|---|---|
| [Products](docs/features/automated-products/index.md) | The generator-driven sample: routes, stored data, events |
| [Purchase Orders](docs/features/manual-purchase-orders/index.md) | The hand-written sample: routes, stored data, events, status lifecycle |
| [Configuration](docs/configuration.md) | Every `appsettings` key a generated service reads, its default and when it takes effect |
| [Documentation index](docs/README.md) | Every guide, the capability-to-attribute tables, and the architecture of a generated solution |
| [Template usage](docs/template-usage.md) | Scaffold parameters; run, test, migrate and publish |
| [DDD implementation guide](docs/ddd-implementation-guide.md) | Adding a vertical slice, entity to endpoint |
| [The shape your endpoints should take](docs/endpoint-shape.md) | One endpoint class per aggregate, and when a route must be hand-written |
| [CRUD attributes](docs/crud-attributes.md) | The four attributes behind the generated slice |
| [Manual vs. Automated](docs/samples/manual-vs-automated.md) | Which sample to copy, and every trade-off behind that call |
| [Extension points](docs/extension-points.md) | Where your own code attaches, and the boundaries the tests hold |
| [Deleting the samples](docs/deleting-the-samples.md) | What to remove once you have copied what you need |
| [AI plugin](docs/ai-plugin.md) | The `dknet-minimal` skills, workflows and subagents, and how the plugin is released |
| [Release notes](docs/release-notes.md) | Behaviour changes a consumer must act on |

There is no deployment guide: this repository deploys nothing — publishing the template package is
its release.

## AI Plugin

Install the `dknet-minimal` agent skills into a generated repository through one of these channels —
what the plugin holds and how it is released: [AI plugin](docs/ai-plugin.md).

**Claude Code**

```text
/plugin marketplace add baoduy/DKNet.Templates
/plugin install dknet-minimal@dknet-marketplace
```

Skills then load on demand as `dknet-minimal:<skill-name>`; workflows are `/dknet-minimal:dknet-feature` etc.

**GitHub Copilot**

```bash
# Copilot reads .agents/skills/ in the workspace; the skills CLI installs there for it
npx skills add baoduy/DKNet.Templates -a github-copilot
```

**Any agent (Codex, Cursor, Gemini CLI, Windsurf, …) via the skills CLI**

```bash
npx skills add baoduy/DKNet.Templates            # pick the skills and the agents to install into
npx skills add baoduy/DKNet.Templates -s '*' -y  # everything, no prompts
```

**From npm (pins the version with your project)**

```bash
npm i -D @drunkcoding/dknet-implementation-skills
claude --plugin-dir node_modules/@drunkcoding/dknet-implementation-skills/plugin   # Claude Code
npx skills experimental_sync -a '*'                                  # any agent: node_modules -> .agents/skills/ etc.
```

## ❓ Open questions

| Question | Why it matters | Checked | Who can answer |
|---|---|---|---|
| Is the template meant to be on nuget.org? The NuGet badge points there, but the package is not published there. | The badge renders as "not found", and the Quick Start needs a GitHub token a reader may not have. | `https://api.nuget.org/v3-flatcontainer/dknet.minimal.template/index.json` returns 404; `publish-nuget-github.yml` publishes to GitHub Packages. | Template maintainer |
| What does a request get when Redis is configured but unreachable — does the idempotency filter fail the create, or let it through? | Decides whether a Redis outage blocks purchase-order creation. | `AppConfig` registers `AddIdempotencyWithRedisStore` with no fallback; the store's failure handling lives in `DKNet.AspCore.Idempotency.RedisStore`, outside this repository. | DKNet.AspCore.Idempotency maintainer |
| What does a caller get when the OIDC metadata endpoint is unreachable while `RequireAuthorization` is on? | Decides whether an identity-provider outage is a `401` for every caller or a `500`. | `AuthConfig` calls `AddJwtBearer()` with no inline options; metadata refresh and failure handling are ASP.NET Core's. Not exercised: the shipped tenant is a placeholder. | Template maintainer |
| Does the host start when `EnableAzureAppConfig` is on and the store is unreachable? | Decides whether an App Configuration outage stops a deployment. | `AzureAppConfigSetup` adds the source without an `optional` flag or a startup timeout; not exercised. | Template maintainer |
| What does the service do when the OTLP collector or Azure Monitor is unreachable? | Decides whether telemetry loss can affect requests. | `LogConfigs` adds the exporters with SDK defaults; not exercised. | Template maintainer |
