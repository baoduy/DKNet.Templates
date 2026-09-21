# Template Usage Reference

How to install `DKNet.Minimal.Template`, scaffold a new solution from it, and run, test, migrate,
and publish that solution.

## Install

```bash
dotnet nuget add source \
  --username <YOUR_GITHUB_USERNAME> \
  --password <YOUR_GITHUB_PAT_WITH_READ_PACKAGES> \
  --store-password-in-clear-text \
  --name github \
  "https://nuget.pkg.github.com/baoduy/index.json"

dotnet new install DKNet.Minimal.Template --nuget-source "https://nuget.pkg.github.com/baoduy/index.json"
```

## Scaffold a solution

```bash
dotnet new dknet-minimal -n MyCompany.MyService
```

A dotted name is supported: `dotnet new dknet-minimal -n DKNet.Accounts` produces a solution that
builds and tests green with no hand edits.

## Scaffold-time parameters

Every parameter below is declared in `src/.template.config/template.json`. Each one carries a
`replaces` token: a literal string that `dotnet new` finds and rewrites throughout the generated
tree. Passing a parameter does not set a variable — it substitutes text, so the default value you
see in this repo is exactly the string that gets replaced.

| Parameter | Flag | Datatype | Default | `replaces` token | What it rewrites, and where |
|---|---|---|---|---|---|
| Name | `-n`, `--name` | string | `MyMinimalApp` | — (`sourceName`: `Minimal`) | Not a `replaces` symbol. `sourceName` rewrites the identifier `Minimal` in every file name, folder name, namespace and project reference, and renames `DKNet.Templates.sln` to `<Name>.sln`. |
| Framework | `--Framework` | choice (`net10.0`) | `net10.0` | `net10.0` | `<TargetFramework>` in `Directory.Packages.props`, and the `HintPath` on the `Aspire.Hosting` reference in `<Name>.AppHost.csproj`. Only `net10.0` is offered — the parameter exists so a later TFM can be added, not so you can target an older one. |
| AuthorName | `--AuthorName` | string | `Steven Hoang` | `Steven Hoang` | `<Authors>` in `Directory.Packages.props`, inherited by every project. |
| CompanyUrl | `--CompanyUrl` | string | `https://drunkcoding.net` | `https://drunkcoding.net` | `<Company>` in `Directory.Packages.props`. |
| RepositoryUrl | `--RepositoryUrl` | string | `https://github.com/baoduy/DKNet` | `https://github.com/baoduy/DKNet` | `<PackageProjectUrl>` and `<RepositoryUrl>` in `Directory.Packages.props`. |
| TenantId | `--TenantId` | string | `00000000-0000-0000-0000-000000000000` | `00000000-0000-0000-0000-000000000000` | `Authentication:Schemes:Bearer:MetadataAddress` and `:ValidIssuer` in `<Name>.Api/appsettings.json`, plus the `PlaceholderTenantGuid` constant in `<Name>.App.Tests/Architecture/AuthPlaceholderConfigTests.cs`. **Replace before enabling authorization.** |
| ApiAudience | `--ApiAudience` | string | `api://your-api` | `api://your-api` | The single entry in `Authentication:Schemes:Bearer:ValidAudiences` in `<Name>.Api/appsettings.json`. **Replace before enabling authorization.** |

```bash
dotnet new dknet-minimal -n Acme.OrderService \
  --AuthorName "Jane Smith" \
  --CompanyUrl "https://acme.com" \
  --RepositoryUrl "https://github.com/acme/order-service" \
  --TenantId "11111111-2222-3333-4444-555555555555" \
  --ApiAudience "api://order-service"
```

### What you must change before shipping

| What | Why | Where to change it after scaffolding |
|---|---|---|
| `--TenantId` | The shipped GUID is not a real tenant. The bearer scheme fetches its OIDC metadata from `https://login.microsoftonline.com/<TenantId>/v2.0/.well-known/openid-configuration`; against a non-existent tenant that fetch never yields signing keys, so no token can be validated. | `Authentication:Schemes:Bearer:MetadataAddress` and `:ValidIssuer` in `<Name>.Api/appsettings.json` |
| `--ApiAudience` | `ValidAudiences` deliberately lists only the API's own audience, so a token issued for any other resource is rejected. Left as `api://your-api`, every real token fails on audience mismatch. | `Authentication:Schemes:Bearer:ValidAudiences` in `<Name>.Api/appsettings.json` |
| `ConnectionStrings:AppDb` | Empty in the base file, and the API cannot open a `DbContext` without it. Supplied automatically only when you launch through the Aspire host. | `<Name>.Api/appsettings.json`, or a `ConnectionStrings__AppDb` environment variable |
| The `RateLimit` numbers | 100 requests and 20 concurrent per second is a placeholder ceiling, not a researched limit for your service. | `RateLimit` in `<Name>.Api/appsettings.json` |

Both auth parameters ship as placeholders on purpose — the template wires no identity provider of
its own. Enabling `FeatureManagement:RequireAuthorization` while they are still in place fails
twice over: the metadata document is fetched from a tenant that does not exist, so the scheme never
obtains its signing keys, and every presented token is rejected on audience mismatch.

> `--TenantId` also rewrites the `PlaceholderTenantGuid` constant inside
> `AuthPlaceholderConfigTests.cs`, because that constant is a literal copy of the token. The test
> keeps passing after scaffolding, but it now pins *your* tenant id rather than guarding a
> placeholder. Full configuration surface, key by key:
> [`configuration-reference.md`](./configuration-reference.md).

Generated layout:

```
MyCompany.MyService/
├── MyCompany.MyService.sln
├── global.json
├── Directory.Packages.props
├── coverage.runsettings
└── MyCompany.MyService.ApiEndpoints/
    ├── MyCompany.MyService.Api/           # Minimal API entry point
    ├── MyCompany.MyService.AppHost/       # .NET Aspire orchestration host
    ├── MyCompany.MyService.AppServices/   # CQRS actions, validators, event handlers
    ├── MyCompany.MyService.Domains/       # Entities, aggregate roots
    ├── MyCompany.MyService.Infra/         # EF Core, repositories, event publisher
    ├── MyCompany.MyService.Share/         # Constants, options, shared base types
    ├── MyCompany.MyService.App.Tests/     # xUnit + Shouldly unit/integration tests
    └── MyCompany.MyService.App.BDDTests/  # Reqnroll + NUnit BDD tests
```

The scaffold also copies `AGENTS.md` into every generated solution — the file list in
`src/DKNet.Minimal.Template.nuspec` is what decides, and it packs nothing else outside `ApiEndpoints/`
and the four solution-level files. `.claude/`, `.claude-plugin/`, `.github/`, `.vscode/` and `.specify/`
stay in this repository: install the AI plugin into the generated repo instead
(`/plugin marketplace add baoduy/DKNet.Templates` + `/plugin install dknet-minimal@dknet-marketplace`,
or `npx skills add baoduy/DKNet.Templates`). See [`template-features.md`](./template-features.md).

## Run

```bash
# API only, no containers
dotnet run --project <Name>.ApiEndpoints/<Name>.Api

# Full stack via Aspire (Redis + PostgreSQL auto-provisioned via Docker)
dotnet run --project <Name>.ApiEndpoints/<Name>.AppHost
```

The Aspire path leaves you a populated database: the migration's three reference purchase orders,
plus 10 000 generated products and 10 000 generated purchase orders, freshly randomised on every
start — with one deliberate exception. Exactly one of those products is fixed, not random:
`Demo-Product-With-Supplier-Data` is the only row carrying both role-gated `[SensitiveData]`
properties, so the response filtering is visible from a running host
([`docs/samples/automated-products/README.md`](./samples/automated-products/README.md#platform-capabilities-it-carries)).
It counts toward the requested total rather than adding to it.

To start empty instead, set `"SampleData": { "RecordsPerEntity": 0 }` in
`<Name>.ApiEndpoints/<Name>.AppHost/appsettings.json` — details in
[`configuration-reference.md`](./configuration-reference.md#sampledata).

## Launch mode: serve, or run a job

One image, one entry point. What the process does is decided by its command-line arguments alone —
no environment name, no configuration key and no build configuration takes part in that decision.

| Arguments | What the process does |
|---|---|
| none | Serves requests, as it always has. |
| a registered job name — `migration` is the one that ships | Runs that job and exits: `0` when it succeeded, non-zero when it failed. No HTTP listener is bound, and no message-bus connection is opened. |
| an argument that is not a registered job name | Exits non-zero without ever serving, naming the jobs it does recognise. |

The job name is the first argument that is neither an option nor the value of one, matched
case-insensitively against the registry in `<Name>.Api/Configs/Jobs/JobRegistry.cs`. An argument
beginning with `-` is an option; when it does not carry its own value with `=`, the argument right
after it is that option's value and is never a job-name candidate. So:

| Arguments | Job name |
|---|---|
| `--urls http://0.0.0.0:8080` | none — serves |
| `--urls http://0.0.0.0:8080 migration` | `migration` |
| `--urls=http://0.0.0.0:8080 migration` | `migration` — the option carries its own value, so it consumes nothing |
| `--some-flag migration` | none — a valueless flag is indistinguishable from `--key value`, so `migration` is read as its value. Pass flags in the `--some-flag=true` form, or put the job name first. |

A job resolves its configuration from exactly the same sources the serving path does, Azure App
Configuration included — job dispatch happens after those sources are added and before any service
is registered.

![Workflow diagram of the launch decision: process arguments reach job-name selection, which takes the first argument that is neither an option nor the value of one; with no job name the service builds the web host, optionally runs the in-process migration gated on RunDbMigrationWhenAppStart, and serves requests; a registered job name runs that job — the shipped migration job migrates and seeds without binding a listener or opening a message bus — and exits 0, or exits non-zero when the migration fails; an unrecognised job name never starts serving and exits non-zero naming the jobs it knows.](diagrams/templates-launch-mode.svg)

```bash
# serves
dotnet run --project <Name>.ApiEndpoints/<Name>.Api

# migrates the database, then exits
dotnet run --project <Name>.ApiEndpoints/<Name>.Api -- migration
```

Adding a second job is one entry in that registry and nothing else — no new branch in the start-up
path, no second project, no second image. See
[`extension-points.md`](./extension-points.md#launch-time-jobs).

### On Kubernetes

The published container's entry point is the application itself, so a container `args` list arrives
as the process arguments. That is the whole deployment shape: one image referenced twice, a `Job`
that passes `migration` and a `Deployment` that passes nothing. Port 8080 below is the ASP.NET Core
container default that `ContainerBaseImage` carries; override it with `ASPNETCORE_HTTP_PORTS`.

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: myservice-migration
spec:
  backoffLimit: 2
  template:
    spec:
      restartPolicy: Never
      containers:
        - name: migration
          image: <your-registry>/myservice-api:<tag>
          args: ["migration"]          # runs the migration job, then exits
          env:
            - name: ConnectionStrings__AppDb
              valueFrom:
                secretKeyRef: { name: myservice-db, key: connection-string }
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myservice-api
spec:
  replicas: 3
  selector:
    matchLabels: { app: myservice-api }
  template:
    metadata:
      labels: { app: myservice-api }
    spec:
      containers:
        - name: api
          image: <your-registry>/myservice-api:<tag>   # same image, no args — serves
          ports:
            - containerPort: 8080
          readinessProbe:
            httpGet: { path: /healthz, port: 8080 }
          env:
            - name: ConnectionStrings__AppDb
              valueFrom:
                secretKeyRef: { name: myservice-db, key: connection-string }
```

The job's exit status is what the cluster acts on: a failed migration fails the `Job` rather than
leaving a pod that came up and quietly serves an un-migrated schema. Sequence the two however your
tooling prefers — a Helm pre-upgrade hook, an Argo CD sync wave, or simply applying the `Job` and
waiting for it before the `Deployment` rollout.

The `Deployment` above sits behind an ingress whose address is not fixed — a pod IP the cluster
assigns from its pod CIDR, not a stable proxy address — so `Security:TrustedProxies` cannot express
it. List the cluster's pod CIDR in `Security:TrustedNetworks` instead, either as an environment
variable on the container (`__` separates the nested keys):

```yaml
          env:
            - name: Security__TrustedNetworks__0
              value: "10.244.0.0/16"
```

or in `appsettings.json`:

```json
"Security": {
  "TrustedNetworks": [ "10.244.0.0/16" ]
}
```

Find the real range for a cluster with `kubectl cluster-info dump | grep -m1 cluster-cidr`, or from
the CNI/cloud provider's networking settings — it is not something the template can default, since
it differs per cluster. Details: [`configuration-reference.md`](configuration-reference.md#security).

### `FeatureManagement:RunDbMigrationWhenAppStart`

The other way to migrate. With the flag on, a serving process migrates the database in-process
before it starts serving — in every environment and in both `Debug` and `Release` builds, with no
argument passed. It ships off, and on in the `Development` overlay
([flag table](./template-features.md#featuremanagement-flags)).

> Earlier versions honoured this flag in `Debug` builds only; a `Release` build ignored it and
> migrated solely on the `migration` argument. It now means the same thing everywhere.

Treat it as a single-process convenience — local work, one-replica deployments, the Aspire host.
Wherever more than one replica starts at once, every replica would race the others through the same
migration: leave the flag off and run the `migration` job instead.

## Test

```bash
dotnet test <Name>.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"

# One project / one feature at a time
dotnet test <Name>.ApiEndpoints/<Name>.App.Tests --filter "FullyQualifiedName~<Feature>"
dotnet test <Name>.ApiEndpoints/<Name>.App.BDDTests --filter "TestCategory=<Feature>"
```

## EF Core migrations

Run these from inside `<Name>.ApiEndpoints/`. The scripts always target `CoreDbContext` in
`<Name>.Infra`.

```bash
./add-migration.sh <MigrationName>
./remove-migration.sh <MigrationName>
```

## Packaging & publishing (template maintainers)

```bash
cd src
dotnet pack DKNet.Minimal.Template.csproj -c Release -o ./nupkgs

# Test locally before publishing
dotnet new install ./nupkgs/DKNet.Minimal.Template.1.0.0.nupkg
dotnet new uninstall DKNet.Minimal.Template

# Publish
dotnet nuget push ./nupkgs/DKNet.Minimal.Template.1.0.0.nupkg \
  --api-key <YOUR_NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```
