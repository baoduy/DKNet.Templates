---
description: "Validate that implemented features follow DKNet DDD conventions across all 4 layers (Domains → Infra → AppServices → Api)"
---

# DKNet Implementation Validator

Validates that all features in this project follow the DKNet.Templates DDD vertical slice conventions.

## User Input

$ARGUMENTS

## Steps

### Step 1: Discover Features

Scan the codebase to find all implemented features:

```bash
# Find all feature entity folders in Domains layer
echo "=== Scanning Domain Entities ==="
find src/ApiEndpoints/Minimal.Domains/Features -name "*.cs" -path "*/Entities/*" | grep -v obj | sort

# Find all feature mapper folders in Infra layer
echo "=== Scanning Infra Mappers ==="
find src/ApiEndpoints/Minimal.Infra/Features -name "*Configs.cs" -path "*/Mappers/*" | grep -v obj | sort

# Find all feature action folders in AppServices layer
echo "=== Scanning AppServices Actions ==="
find src/ApiEndpoints/Minimal.AppServices -name "*.cs" -path "*/Actions/*" | grep -v obj | sort

# Find all endpoint configs in Api layer
echo "=== Scanning API Endpoints ==="
find src/ApiEndpoints/Minimal.Api/ApiEndpoints -name "*Endpoint.cs" | grep -v obj | sort
```

### Step 2: Validate Each Feature — Domain Layer

For each entity found in Step 1, verify:

**2.1 Entity Inheritance**
- Entity class MUST inherit from `AggregateRoot` (for root entities) or `DomainEntity` (for non-root)
- Entity MUST NOT be `sealed` (base class hierarchy requires inheritance)
- Check: `grep -n "class.*: AggregateRoot\|class.*: DomainEntity" <entity_file>`

**2.2 Property Encapsulation**
- All properties MUST use `{ get; private set; }` — NO public setters
- Check: entity should NOT contain `{ get; set; }` on domain properties (except owned types)
- Check: `grep -n "{ get; set; }" <entity_file>` should return 0 matches for aggregate root properties

**2.3 Constructor Pattern**
- Public constructor MUST pass `Guid.Empty` as first argument (new entity)
- Internal constructor MUST accept `Guid id` (rehydration from persistence)
- Constructor MUST call `base(id, createdBy)`
- Check: `grep -n "internal.*Guid id" <entity_file>`

**2.4 Mutation Methods**
- Mutable fields MUST only change through named `Update(...)` or domain methods
- Update method MUST call `SetUpdatedBy(userId)` at the end
- Check: `grep -n "SetUpdatedBy" <entity_file>`

**2.5 Schema Registration**
- Entity feature MUST have a schema constant in `DomainSchemas.cs`
- Check: `grep -c "public const string" src/ApiEndpoints/Minimal.Domains/Share/DomainSchemas.cs`

### Step 3: Validate Each Feature — Infrastructure Layer

**3.1 Mapper Class**
- Mapper MUST be `internal sealed`
- Mapper MUST inherit from `DefaultEntityTypeConfiguration<TEntity>` (NOT `IEntityTypeConfiguration`)
- Mapper MUST call `base.Configure(builder)` as the FIRST line in Configure method
- Check: `grep -n "internal sealed class.*: DefaultEntityTypeConfiguration" <mapper_file>`
- Check: `grep -n "base.Configure" <mapper_file>`

**3.2 Mapper Placement**
- File MUST be in `Minimal.Infra/Features/{Feature}/Mappers/`
- Namespace MUST match folder: `Minimal.Infra.Features.{Feature}.Mappers`
- Check: `grep -n "namespace.*Infra.*Features.*Mappers" <mapper_file>`

**3.3 Property Configuration**
- All string properties MUST have `HasMaxLength()`
- Required/Optional MUST be explicitly set with `IsRequired()` / `IsRequired(false)`
- Table MUST be mapped with schema: `ToTable("Name", DomainSchemas.{Schema})`
- Check: `grep -c "HasMaxLength" <mapper_file>` should equal number of string properties
- Check: `grep -n "ToTable.*DomainSchemas" <mapper_file>`

**3.4 Service Implementations (if domain services exist)**
- Services MUST be `internal sealed`
- Services MUST be in `Minimal.Infra.Services` namespace (for Scrutor auto-discovery)
- Check: `grep -rn "internal sealed class.*: I.*Service" src/ApiEndpoints/Minimal.Infra/Services/`

### Step 4: Validate Each Feature — AppServices Layer

**4.1 Response DTO**
- MUST use `[GenerateDto(typeof(Entity))]` attribute
- MUST use `[MapsFrom(typeof(Entity))]` attribute
- MUST be `sealed partial record`
- Check: `grep -n "GenerateDto\|MapsFrom\|sealed partial record" <dto_file>`

**4.2 Action Request Types**
- Create request: MUST implement `Fluents.Requests.IWitResponse<TDto>` and extend `RequestBase`
- Update request: MUST implement `Fluents.Requests.IWitResponse<TDto>` and extend `RequestBase`
- Delete request: MUST implement `Fluents.Requests.INoResponse` and extend `RequestBase`
- MUST have `[MapsFrom(typeof(Entity))]` on Create request
- Check: `grep -n "IWitResponse\|INoResponse\|RequestBase" <action_files>`

**4.3 Validators**
- Each request DTO with input fields MUST have a corresponding validator
- Validator MUST be `internal sealed` and extend `AbstractValidator<TRequest>`
- Check: `grep -rn "internal sealed class.*Validator.*AbstractValidator" <feature_actions_dir>`

**4.4 Handlers**
- Handlers MUST be `internal sealed`
- Handlers MUST use primary constructor injection
- Handlers MUST use `IRepositorySpec` (NOT custom repository interfaces)
- Create handler MUST check for duplicates via Specification before adding
- Create handler SHOULD use `mapper.ResultOf<T>()` for lazy mapping
- Update handler MUST call entity mutation method (not set properties directly)
- Delete handler MUST return `IResultBase`
- Check: `grep -n "IRepositorySpec\|ResultOf\|Result.Fail\|Result.Ok" <handler_files>`

**4.5 Query Specifications**
- MUST be `internal sealed` extending `Specification<TEntity>`
- MUST use predicate builder pattern: `CreatePredicate().And(...)`
- Check: `grep -rn "internal sealed class.*Specification" <feature_specs_dir>`

**4.6 Domain Events**
- Events MUST be `sealed record` types
- Event handlers MUST implement `Fluents.EventsConsumers.IHandler<TEvent>`
- Check: `grep -rn "sealed record.*Event\|EventsConsumers.IHandler" <feature_events_dir>`

### Step 5: Validate Each Feature — API Layer

**5.1 Endpoint Config Class**
- MUST be `internal sealed` implementing `IEndpointConfig`
- MUST have `Version` (int) and `GroupEndpoint` (string with leading `/`)
- `GroupEndpoint` MUST use kebab-case
- Check: `grep -n "internal sealed class.*IEndpointConfig\|GroupEndpoint\|Version =>" <endpoint_file>`

**5.2 Fluent Endpoint Mapping**
- MUST use fluent helpers: `MapGetList`, `MapGetById`, `MapPost`, `MapPut`, `MapDelete`
- MUST NOT use raw `app.MapGet/MapPost` calls
- All endpoints MUST have `.WithDescription()` for OpenAPI docs
- Check: `grep -c "MapGetList\|MapGetById\|MapPost\|MapPut\|MapDelete" <endpoint_file>`
- Check: `grep -c "WithDescription" <endpoint_file>`

### Step 6: Cross-Layer Consistency

**6.1 Namespace Alignment**
- Verify feature name is consistent across all layers:
  - Domain: `Minimal.Domains.Features.{Feature}.Entities`
  - Infra: `Minimal.Infra.Features.{Feature}.Mappers`
  - AppServices: `Minimal.AppServices.{Feature}.V{N}.Actions`
  - Api: `Minimal.Api.ApiEndpoints`

**6.2 Entity Coverage**
- Every domain entity MUST have a corresponding mapper in Infra
- Every entity with CRUD MUST have at least Create + Delete actions
- Every entity with actions MUST have at least one endpoint config

**6.3 Build Verification**

```bash
dotnet build src/DKNet.Templates.sln -c Release
```

Must complete with zero errors and zero warnings.

### Step 7: Generate Report

Produce a validation report:

```markdown
# DKNet Convention Validation Report

## Summary
| Layer | Features Found | Pass | Fail | Warnings |
|-------|---------------|------|------|----------|
| Domain | {n} | {n} | {n} | {n} |
| Infra | {n} | {n} | {n} | {n} |
| AppServices | {n} | {n} | {n} | {n} |
| Api | {n} | {n} | {n} | {n} |
| Cross-Layer | - | {n} | {n} | {n} |

## Build Status: {PASS/FAIL}

## Details

### {FeatureName}

#### Domain Layer
- [X] Entity inherits AggregateRoot
- [X] Properties use private setters
- [ ] FAIL: Missing SetUpdatedBy in Update method
...

#### Infrastructure Layer
...

#### AppServices Layer
...

#### API Layer
...

## Recommendations
1. {Specific fix instructions for each failure}
```

Save the report to `{FEATURE_DIR}/validation-report.md` if a spec feature directory exists, otherwise print to console.
