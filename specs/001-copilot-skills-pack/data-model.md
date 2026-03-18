# Phase 1 Data Model: Skill Definitions & Architecture

**Date**: 2026-03-17  
**Output of**: `/speckit.plan` command Phase 1 section  
**Status**: Complete  

## Overview

This document defines the data model for the Copilot Skills Pack:
- **Skill entities**: Three foundational skill definitions with metadata
- **Skill catalog structure**: How skills are indexed and discovered
- **Relationships**: Dependencies between skills
- **Validation model**: Checklist-based quality gates

---

## Skill Entities

### Skill 1: Domain Modeling with EFCore Mapping Configuration

```yaml
Skill:
  id: domain-modeling
  title: "EFCore Mapping Configuration Skill"
  description: "Guide developers through creating domain entities and their EF Core persistence mappings using auto-configuration patterns"
  folder: ".github/skills/domain-modeling/"
  
Metadata:
  category: "Persistence & Entities"
  difficulty: "Intermediate"
  estimatedDuration: "20-30 minutes"
  
Prerequisites:
  - Understand DKNet.Templates vertical slice architecture (read AGENTS.md)
  - Familiar with C# entity classes and inheritance
  - Know basic EF Core model configuration concepts
  
Inputs:
  description: "Information provided by developer before starting skill"
  items:
    - Domain entity class name (e.g., "CustomerProfile")
    - Entity properties (field names, C# types)
    - Validation rules (field lengths, required/optional)
    - Database table/schema mapping details (if custom)
    - Relationships (foreign keys, navigation properties, cardinality)
  
Outputs:
  description: "Deliverables after completing skill"
  items:
    - Mapper class (e.g., "CustomerProfileMapper") inheriting from base auto-config
    - EF Core migration script (if new table or major schema change)
    - Index definitions (for performance-critical query paths)
    - Validation constraints (encoded in mapping)
    - XML documentation comments (if non-obvious rules)
  
SuccessCriteria:
  - "[✓] Mapper class follows ProfileMapper template pattern from AGENTS.md"
  - "[✓] Class is placed in Minimal.Infra/Features/<Feature>/Mappers/ (auto-discovery)"
  - "[✓] All entity properties correctly mapped to database types (nvarchar, int, datetime, etc.)"
  - "[✓] Validation rules (lengths, nullability) enforced in mapping (ConfigureProperty)"
  - "[✓] Foreign keys and navigation properties configured (HasOne, HasMany)"
  - "[✓] Mapper class is sealed (for Scrutor auto-registration)"
  - "[✓] Migration script applies without errors: ./add-migration.sh <MigrationName>"
  - "[✓] Schema matches Entity Framework model (run and verify with Update-Database)"
  - "[✓] Performance indexes added for common query filters (if applicable)"
  - "[✓] Code compiles without warnings (warnings-as-errors enforced in CI)"
  
NonGoals:
  - Does NOT cover repository pattern (see CRUD Operations Skill)
  - Does NOT cover queries/specifications (see query design section of CRUD Skill)
  - Does NOT cover async/await patterns (baseline knowledge; covered in CRUD Skill if needed)
  - Does NOT design REST endpoints (see API Endpoints Skill)
  - Does NOT cover advanced EF concepts (Table-per-type inheritance, shadow properties, etc.)
  
Example:
  path: "examples/customer-profile-example/"
  files:
    - "CustomerProfile.cs" → domain entity with properties
    - "CustomerProfileMapper.cs" → EF Core configuration (the main output)
    - "README.md" → explanation of the example
  
TestPath: "src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/DomainModelingSkillTests.cs"
```

### Skill 2: CRUD Operations Implementation

```yaml
Skill:
  id: crud-operations
  title: "CRUD Operations Implementation Skill"
  description: "Build Create/Read/Update/Delete workflows with encapsulated domain mutations, commands, and event publishing"
  folder: ".github/skills/crud-operations/"
  
Metadata:
  category: "Business Logic & Commands"
  difficulty: "Intermediate-Advanced"
  estimatedDuration: "45-60 minutes for complete CRUD (adjustable if partial)"
  
Prerequisites:
  - Completed Domain Modeling Skill OR have an mapped entity ready
  - Understand BaseCommand + ISpecification patterns (read AGENTS.md section "Commands, mapping...")
  - Know DKNet.Templates layer boundaries: Api → AppServices → Domains → Infra
  - Familiar with domain events and EventPublisher (AGENTS.md section "Message bus and events")
  - xUnit + Shouldly testing patterns (AGENTS.md section "Testing and quality...")
  
Inputs:
  description: "Information provided by developer before starting skill"
  items:
    - Domain entity to build CRUD for (e.g., "CustomerProfile")
    - Create/Read/Update/Delete requirements (which operations are needed)
    - Business rules and validation logic for each operation
    - Event triggers (which operations should publish domain events)
    - Permission/authorization requirements (if user-scoped, e.g., "only owner can update")
  
Outputs:
  description: "Deliverables after completing skill"
  items:
    - Entity class with encapsulated mutation methods (Create(), Update(), Delete())
    - Create command + handler (CreateProfileCommand in AppServices)
    - Update command + handler (UpdateProfileCommand in AppServices)
    - (Optional) Delete command + handler
    - Specifications/queries for data access (GetProfileByIdSpec, etc.)
    - Repository interface + implementation (sealed class)
    - Domain events (ProfileCreatedEvent, ProfileUpdatedEvent, etc.)
    - (Optional) Event subscribers/handlers (if cross-feature consumption)
    - Unit tests covering happy path + error scenarios (xUnit + Shouldly)
    - (Optional) Integration tests verifying end-to-end workflows
  
SuccessCriteria:
  - "[✓] Entity class encapsulates all mutations (Create(), Update(), Delete() methods; no setters on domain properties)"
  - "[✓] Commands inherit from BaseCommand; handlers live in AppServices layer"
  - "[✓] Validation rules enforced at command level (FluentValidation) AND entity level (domain logic)"
  - "[✓] Domain events published via EventPublisher.Publish() after each mutation"
  - "[✓] Entities follow class-first design: business rules are methods, not anemic data"
  - "[✓] Repository interface defined in Domains; implementation sealed in Infra"
  - "[✓] Repository sealed + placed in Minimal.Infra/Features/<Feature>/Repos/ (auto-discovered by Scrutor)"
  - "[✓] Specifications/queries implement ISpecification pattern"
  - "[✓] All CRUD paths covered by unit tests; assertions use Shouldly (.Should().Be(...) pattern)"
  - "[✓] Test coverage >80% for domain entity and commands"
  - "[✓] Code compiles without warnings; passes warnings-as-errors check"
  
NonGoals:
  - Does NOT design REST endpoints (see API Endpoints Skill)
  - Does NOT cover caching strategies or query optimization beyond basic indexes
  - Does NOT implement API authentication/authorization (covered elsewhere)
  - Does NOT cover event sourcing patterns (covered in advanced skill in future)
  - Does NOT design complex state machines (beyond simple Create → Published transitions)
  
Example:
  path: "examples/customer-profile-crud/"
  files:
    - "CustomerProfile.cs" → entity with Create(), Update(), Delete() methods
    - "CreateProfileCommand.cs" + "CreateProfileCommandHandler.cs"
    - "UpdateProfileCommand.cs" + "UpdateProfileCommandHandler.cs"
    - "GetProfileByIdSpec.cs" → specification for querying
    - "ProfileRepository.cs" → repository interface + sealed implementation
    - "ProfileCreatedEvent.cs" → domain event
    - "ProfileCreatedEventConsumer.cs" → event handler (optional example)
    - "CustomerProfileTests.cs" → unit tests
    - "README.md" → explanation
  
TestPath: "src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/CrudOperationsSkillTests.cs"
```

### Skill 3: API REST Endpoints Configuration

```yaml
Skill:
  id: api-endpoints
  title: "API REST Endpoints Configuration Skill"
  description: "Wire domain commands and queries to HTTP endpoints using fluent mappers, DTOs, and OpenAPI documentation"
  folder: ".github/skills/api-endpoints/"
  
Metadata:
  category: "REST API & Orchestration"
  difficulty: "Beginner-Intermediate"
  estimatedDuration: "30-40 minutes for standard CRUD endpoints"
  
Prerequisites:
  - Completed CRUD Operations Skill (or have commands/specs ready)
  - Understand ASP.NET Minimal APIs (basic GET/POST/PUT/DELETE routing)
  - Know fluent mapper pattern (FluentEndpointMapperExtensions from AGENTS.md)
  - Familiar with OpenAPI/Swagger documentation basics
  - Understand DTOs and [GenerateDto] attribute for Mapster (AGENTS.md section "Commands, mapping...")
  
Inputs:
  description: "Information provided by developer before starting skill"
  items:
    - Command and Specification classes (from CRUD Operations Skill)
    - Entity DTOs (using [GenerateDto] attribute; Mapster auto-generates)
    - Route paths and HTTP methods (GET, POST, PUT, DELETE)
    - Authentication/authorization requirements (if any)
    - OpenAPI documentation (endpoint summary, parameters, response codes)
    - Pagination requirements (if listing endpoints)
  
Outputs:
  description: "Deliverables after completing skill"
  items:
    - Endpoint configuration class implementing IEndpointConfig (e.g., ProfileV1Endpoints)
    - Endpoint group mapping (using CreateGroup() fluent helpers)
    - Request/response DTOs with proper attributes ([FromBody], [FromRoute], etc.)
    - OpenAPI/Swagger documentation annotations (ProduceResponseType, etc.)
    - Integration tests verifying endpoints work end-to-end (happy path + errors)
    - (Optional) Versioning documentation if multiple API versions
  
SuccessCriteria:
  - "[✓] Endpoints inherit from IEndpointConfig pattern (matches ProfileV1Endpoint template)"
  - "[✓] Uses fluent mappers: MapGetList(), MapGetById(), MapPost(), MapPut(), MapDelete()"
  - "[✓] DTOs decorated with [GenerateDto(...)] attribute; Mapster auto-generates implementation"
  - "[✓] All endpoints documented with OpenAPI/Swagger descriptions and response status codes"
  - "[✓] Integration tests cover happy path + error scenarios (invalid input, not found, unauthorized, etc.)"
  - "[✓] Request/response structures match OpenAPI contract documentation"
  - "[✓] Route paths follow REST conventions (/api/v1/profiles, /api/v1/profiles/{id}, etc.)"
  - "[✓] Validation errors return 400 BadRequest with descriptive error messages"
  - "[✓] Not-found cases return 404 NotFound (not generic 500 error)"
  - "[✓] Code compiles without warnings; integration tests pass"
  
NonGoals:
  - Does NOT implement complex business rules (see CRUD Operations Skill)
  - Does NOT design database schema (see Domain Modeling Skill)
  - Does NOT cover advanced authentication/authorization (e.g., RBAC, scopes)
  - Does NOT design complex OpenAPI features (discriminators, schema inheritance, etc.)
  - Does NOT implement rate limiting or caching (covered in separate infrastructure skills)
  
Example:
  path: "examples/customer-profile-endpoints/"
  files:
    - "ProfileV1Endpoints.cs" → IEndpointConfig implementation with MapPost, MapPut, etc.
    - "ProfileRequestDto.cs" → request DTO with [GenerateDto]
    - "ProfileResponseDto.cs" → response DTO with [GenerateDto]
    - "ProfileEndpointsTests.cs" → integration tests (REST API calls)
    - "README.md" → explanation + example curl commands
  
TestPath: "src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/ApiEndpointsSkillTests.cs"
```

---

## Skill Catalog Structure

### Catalog Entity Model

```yaml
Catalog:
  skills: [
    {
      id: "domain-modeling",
      title: "EFCore Mapping Configuration",
      category: "Persistence & Entities",
      difficulty: "Intermediate",
      durationMinutes: { min: 20, max: 30 },
      useWhen: "Adding a new domain entity that requires database persistence",
      exampleEntity: "CustomerProfile",
      folderPath: ".github/skills/domain-modeling/",
      prerequisites: [
        "Read AGENTS.md for architecture overview",
        "Understand C# classes and inheritance"
      ],
      relatedSkills: ["crud-operations"],
      nextSkill: "crud-operations"
    },
    {
      id: "crud-operations",
      title: "CRUD Operations Implementation",
      category: "Business Logic & Commands",
      difficulty: "Intermediate-Advanced",
      durationMinutes: { min: 45, max: 60 },
      useWhen: "Implementing create, read, update, delete logic for an entity",
      exampleEntity: "CustomerProfile",
      folderPath: ".github/skills/crud-operations/",
      prerequisites: [
        "Completed Domain Modeling Skill",
        "Understand BaseCommand pattern",
        "Know DKNet.Templates layer boundaries"
      ],
      relatedSkills: ["domain-modeling", "api-endpoints"],
      previousSkill: "domain-modeling",
      nextSkill: "api-endpoints"
    },
    {
      id: "api-endpoints",
      title: "API REST Endpoints Configuration",
      category: "REST API & Orchestration",
      difficulty: "Beginner-Intermediate",
      durationMinutes: { min: 30, max: 40 },
      useWhen: "Wiring domain commands and queries to HTTP endpoint routes",
      exampleEntity: "CustomerProfile",
      folderPath: ".github/skills/api-endpoints/",
      prerequisites: [
        "Completed CRUD Operations Skill",
        "Understand ASP.NET Minimal APIs",
        "Know fluent mapper pattern"
      ],
      relatedSkills: ["crud-operations"],
      previousSkill: "crud-operations"
    }
  ]
  
RecommendedWorkflows: [
    {
      name: "New Feature (Full Vertical Slice)",
      steps: ["domain-modeling", "crud-operations", "api-endpoints"],
      totalDuration: "120 minutes"
    },
    {
      name: "Read-Only Entity with Endpoints",
      steps: ["domain-modeling", "api-endpoints"],
      totalDuration: "70 minutes",
      notes: "Skip CRUD if no mutations needed (e.g., reporting entity)"
    },
    {
      name: "Logic-Only Change",
      steps: ["crud-operations"],
      totalDuration: "45-60 minutes",
      notes: "If entity + endpoints already exist; only need to add/modify commands"
    }
  ]
```

### Discover Paths

**Path 1: Search CATALOG.md**
- Developer opens `.github/skills/CATALOG.md`
- Searches for keyword "entity" → finds all three skills
- Picks based on "Use When" column
- Time to discover: <1 minute

**Path 2: Follow Recommended Workflow**
- Developer starts with `.github/skills/README.md`
- Sees "New Feature (Full Vertical Slice)" workflow
- Follows steps 1 → 2 → 3 in order
- Time to discover: <2 minutes (reading README)

**Path 3: Copilot Chat Command**
- Developer: `@skills domain-modeling`
- Copilot returns skill metadata + link to skill.md
- Time to discover: <30 seconds

---

## Validation Model: Skill Quality Gates

Each skill includes a `checklist.md` file with gates developers must satisfy:

### Gate Types

1. **Pre-Implementation Gates**
   - Prerequisites satisfied? (docs read, knowledge verified)
   - Inputs identified? (entity name, properties listed)
   
2. **Implementation Validation Gates**
   - Code follows template pattern? (matches ProfileMapper, ProfileV1Endpoints, etc.)
   - Located in correct folder? (auto-discovery folders respected)
   - Naming conventions followed? (PascalCase, sealed classes, etc.)
   
3. **Quality Gates (MUST PASS)**
   - Compiles without warnings? (CI check: warnings-as-errors)
   - Tests pass? (unit/integration tests from example)
   - Example test coverage >80%? (for CRUD skills)
   - Code follows layer boundaries? (no business logic in Api, etc.)
   
4. **Documentation Gates**
   - Code is self-documenting? (property/method names are clear)
   - Complex logic explained? (XML comments on non-obvious methods)
   - Example reference available? (link to worked example)

### Validation Automation

CI script (`.github/workflows/validate-skills.yaml`):
```bash
#!/bin/bash
# For each skill in .github/skills/*/
for skill in .github/skills/*/; do
  # 1. Validate metadata.json against schema
  json-schema-validator .github/skills/contracts/skill-schema.json "$skill/metadata.json"
  
  # 2. Verify required files present
  [ -f "$skill/skill.md" ] || exit 1
  [ -f "$skill/metadata.json" ] || exit 1
  [ -f "$skill/checklist.md" ] || exit 1
  [ -d "$skill/templates" ] || exit 1
  [ -d "$skill/examples" ] || exit 1
  
  # 3. Run example tests
  dotnet test "$skill/examples/*Tests.cs" || exit 1
done

# 4. Auto-generate catalog from metadata
python3 .github/scripts/generate-catalog.py
```

---

## Entity Relationships

```
Skill (1)
  ├─ has-many: Templates (0..*)
  ├─ has-one: Example (1)
  │   └─ has-many: Tests (1..*)
  ├─ has-one: Metadata (1)
  └─ has-one: Validation Checklist (1)

Catalog
  ├─ contains: Skill Summaries (1..N)
  └─ contains: Workflows (1..N)
    └─ references: Skill dependency chain
```

---

## Key Constraints

1. **Folder Structure Immutable** (once published)
   - Don't rename or move skill folders (breaks catalogs, documentation, CI scripts)
   - If significant redesign needed, create Skill v2 instead (e.g., `crud-operations-v2/`)
   
2. **Metadata Always in Sync**
   - metadata.json must match skill.md content
   - If duration changes, update both files
   - CI validation checks for drift
   
3. **Examples Always Working**
   - Example code must compile and tests must pass
   - CI builds/tests examples on every commit
   - If framework version bumps, examples updated in same commit
   
4. **Backward Compatibility**
   - Don't delete success criteria from checklist.md (add new ones, keep old ones)
   - Don't change prerequisite knowledge dramatically (version skill instead)
   - Don't move/rename template files (links will break)

---

## Future Extension Points

These are NOT in scope for MVP but documented for Phase 2+ extensibility:

- **Skill Versioning**: `domain-modeling-v1/`, `domain-modeling-v2/` if design changes significantly
- **Skill Tagging**: Add tags for filtering (e.g., #testing, #performance, #advanced)
- **Skill Dependencies**: Formalize "must complete X before Y" (currently documented in metadata)
- **Skill Variants**: Domain modeling for EF Core vs. cosmos DB vs. NoSQL (future skill variants)
- **Interactive Workflows**: Copilot Agent that guides through skill step-by-step (beyond static documentation)
- **Telemetry**: Track which skills are most used, where developers struggle
- **Community Skills**: Accept contributed skills from team members (with review process)

