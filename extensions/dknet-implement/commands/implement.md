---
description: "Execute feature implementation using DKNet skills in the correct DDD layer order (Domain → Infra → AppServices → Api)"
---

# DKNet Guided Implementation

Implements a feature specification by guiding the developer through all 4 DDD layers in the correct order, using the project's Claude Code skills.

## User Input

$ARGUMENTS

## Prerequisites

- A feature specification exists (run `/speckit.specify` first if not)
- A task plan exists in `tasks.md` (run `/speckit.tasks` first if not)
- The developer has gathered the inputs listed in each skill

## Steps

### Step 1: Load Feature Context

```bash
# Check for spec artifacts
FEATURE_DIR=$(find specs/ -maxdepth 1 -type d | tail -1)
echo "Feature directory: $FEATURE_DIR"

# Read available artifacts
for f in spec.md plan.md tasks.md data-model.md contracts/*.md; do
  if [ -f "$FEATURE_DIR/$f" ]; then
    echo "Found: $f"
  fi
done
```

Read and analyze:
- **REQUIRED**: `tasks.md` — the implementation task list
- **REQUIRED**: `plan.md` — architecture and file structure
- **IF EXISTS**: `data-model.md` — entity definitions and relationships
- **IF EXISTS**: `spec.md` — requirements and acceptance criteria
- **IF EXISTS**: `contracts/` — API contracts and test requirements

### Step 2: Extract Entity Definitions

From the feature context, extract for each entity:
1. Entity name (PascalCase)
2. Feature folder name (plural)
3. Properties (name, type, required/optional)
4. Mutation rules (what changes after creation)
5. Schema prefix
6. Unique constraints
7. Business rules (duplicate checks, validation)
8. Events to publish

### Step 3: Execute Layer 1 — Domain Entity

**Skill**: `.claude/skills/dknet-domain-entity/SKILL.md`

For each entity identified in Step 2:

1. Add schema constant to `DomainSchemas.cs`
2. Add sequence name to `Sequences.cs` (if auto-ID needed)
3. Create entity class inheriting `AggregateRoot`:
   - File: `src/ApiEndpoints/Minimal.Domains/Features/{Feature}/Entities/{Entity}.cs`
   - Two constructors: public (Guid.Empty) + internal (rehydration)
   - Properties with `{ get; private set; }`
   - `Update(...)` method calling `SetUpdatedBy()`
4. Create domain service interface (if needed):
   - File: `src/ApiEndpoints/Minimal.Domains/Services/I{Service}.cs`
5. Create owned value objects (if needed):
   - File: `src/ApiEndpoints/Minimal.Domains/Features/{Feature}/Entities/{OwnedType}.cs`

**Checkpoint**: `dotnet build src/ApiEndpoints/Minimal.Domains/Minimal.Domains.csproj`

### Step 4: Execute Layer 2 — EF Core Configuration

**Skill**: `.claude/skills/dknet-efcore-config/SKILL.md`

For each entity:

1. Create mapper class:
   - File: `src/ApiEndpoints/Minimal.Infra/Features/{Feature}/Mappers/{Entity}Configs.cs`
   - `internal sealed class` inheriting `DefaultEntityTypeConfiguration<{Entity}>`
   - Call `base.Configure(builder)` first
   - Configure indexes, max lengths, column types
   - Map to table with schema: `ToTable("{Table}", DomainSchemas.{Schema})`
2. Implement domain services (if interfaces exist):
   - File: `src/ApiEndpoints/Minimal.Infra/Services/{Service}.cs`
   - `internal sealed class` in `.Services` namespace
3. Add static seed data (if needed):
   - File: `src/ApiEndpoints/Minimal.Infra/Features/{Feature}/StaticData/{Entity}StaticData.cs`
4. Register owned types in `OwnedDataContext.cs` (if applicable)

**Checkpoint**: `dotnet build src/ApiEndpoints/Minimal.Infra/Minimal.Infra.csproj`

5. Run migration:
```bash
cd src/ApiEndpoints
./add-migration.sh {FeatureName}Init
```

### Step 5: Execute Layer 3 — AppServices Actions

**Skill**: `.claude/skills/dknet-appservices-actions/SKILL.md`

For each entity:

1. Create response DTO:
   - File: `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/{Entity}Dto.cs`
   - `[GenerateDto(typeof({Entity}))]` + `[MapsFrom(typeof({Entity}))]`
   - `sealed partial record`

2. Create actions (one file per operation):
   - `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Create.cs`
     - Request: `sealed record` + `RequestBase` + `Fluents.Requests.IWitResponse<{Entity}Dto>`
     - Validator: `internal sealed` + `AbstractValidator<T>`
     - Handler: `internal sealed` + `Fluents.Requests.IHandler<TReq, TDto>`
       - Check duplicates → map → persist → add event → `mapper.ResultOf<T>()`
   - `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Update.cs`
     - Request: `record` + `RequestBase` + `Fluents.Requests.IWitResponse<{Entity}Dto>`
     - Handler: fetch via spec → call `entity.Update(...)` → `mapper.Map<T>(entity)`
   - `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Delete.cs`
     - Request: `record` + `RequestBase` + `Fluents.Requests.INoResponse`
     - Handler: fetch via spec → `repository.Delete(entity)` → `Result.Ok()`

3. Create query specification:
   - File: `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Specs/SpecGet{Entity}.cs`
   - `internal sealed class` extending `Specification<{Entity}>`
   - Predicate builder for Id + unique field lookups

4. Create domain events:
   - File: `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Events/{Entity}CreatedEventHandlers.cs`
   - Event: `sealed record`
   - Handler: `Fluents.EventsConsumers.IHandler<TEvent>`

5. Add entity to GlobalUsings.cs (if frequently used)

**Checkpoint**: `dotnet build src/ApiEndpoints/Minimal.AppServices/Minimal.AppServices.csproj`

### Step 6: Execute Layer 4 — Endpoint Configuration

**Skill**: `.claude/skills/dknet-endpoint-config/SKILL.md`

For each entity:

1. Create endpoint config:
   - File: `src/ApiEndpoints/Minimal.Api/ApiEndpoints/{Entity}V1Endpoint.cs`
   - `internal sealed class` implementing `IEndpointConfig`
   - Set `Version => 1` and `GroupEndpoint => "/{kebab-case}"`
   - Map: `MapGetList`, `MapGetById`, `MapPost`, `MapPut`, `MapDelete`
   - Add `.WithDescription()` on all endpoints
   - Add `.AddIdempotencyFilter()` on POST if needed
   - Add DTO type alias if namespace conflicts

**Checkpoint**: `dotnet build src/DKNet.Templates.sln -c Release`

### Step 7: Full Build + Test Verification

```bash
# Full solution build
dotnet build src/DKNet.Templates.sln -c Release

# Run tests
dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings --collect:"XPlat Code Coverage"
```

Both MUST pass with zero errors.

### Step 8: Run Convention Validation

Execute the DKNet validation command to verify all conventions are followed:

```
/speckit.dknet-implement.validate
```

Address any failures before considering the implementation complete.

### Step 9: Update Tasks

Mark all completed tasks as `[X]` in `tasks.md`.

## Implementation Order Summary

```
1. Domain Entity     → Minimal.Domains/Features/{F}/Entities/
2. Schema Constant   → Minimal.Domains/Share/DomainSchemas.cs
3. EF Core Mapper    → Minimal.Infra/Features/{F}/Mappers/
4. Infra Services    → Minimal.Infra/Services/
5. DB Migration      → ./add-migration.sh
6. Response DTO      → Minimal.AppServices/{F}/V1/{Entity}Dto.cs
7. Create Action     → Minimal.AppServices/{F}/V1/Actions/Create.cs
8. Update Action     → Minimal.AppServices/{F}/V1/Actions/Update.cs
9. Delete Action     → Minimal.AppServices/{F}/V1/Actions/Delete.cs
10. Query Spec       → Minimal.AppServices/{F}/V1/Specs/
11. Domain Events    → Minimal.AppServices/{F}/V1/Events/
12. Endpoint Config  → Minimal.Api/ApiEndpoints/{Entity}V1Endpoint.cs
13. Build + Test     → dotnet build && dotnet test
14. Validate         → /speckit.dknet-implement.validate
```

## Notes

- Always build after completing each layer to catch errors early
- If build fails at any checkpoint, fix before proceeding to next layer
- The validation command runs automatically after implementation (via `after_implement` hook)
- For features with multiple entities, complete ALL entities in one layer before moving to the next
