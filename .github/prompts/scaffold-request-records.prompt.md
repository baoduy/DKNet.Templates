---
description: "Scaffold Create/Update request records for AppServices using GenerateDto-first policy and a mandatory writable-field audit section."
argument-hint: "Entity, feature, and operation scope. Example: CustomerProfile create/update requests for V1"
agent: "agent"
---
Generate AppServices request records for the provided feature and entity following DKNet conventions.

## Inputs
- Entity name and namespace
- Feature folder and version
- Operation scope: Create, Update, or both
- Required/optional writable fields
- Business constraints (validation, uniqueness, immutability)

## Must Follow
- Apply GenerateDto-first policy:
  - response DTOs should use `[GenerateDto]` by default
  - manual request records only for intentional contract divergence
- Keep `[MapsFrom(typeof(Entity))]` where applicable
- Keep field names/types aligned with entity unless divergence is intentional

## Output Required
1. `Create*Request` and/or `Update*Request` records
2. Matching validators (`AbstractValidator<TRequest>`)
3. Handler signatures (only minimal skeletons if not requested fully)
4. A mandatory `Writable Field Audit` section in markdown:
   - Writable fields
   - Server-managed fields (Id, audit, sequence, ByUser)
   - Divergence notes and rationale

## Quality Gate
Before finishing, verify:
- Any manual DTO/request that mirrors entity shape has an explicit reason not to use GenerateDto
- Validation rules match field requiredness and constraints
- Namespaces/paths follow `src/ApiEndpoints/Minimal.AppServices/<Feature>/V1/Actions/`
