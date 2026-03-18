# Implementation Plan: Reusable Copilot Skills Pack

**Branch**: `001-copilot-skills-pack` | **Date**: 2026-03-17 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-copilot-skills-pack/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command.

## Summary

Build a discoverable, maintainable library of three reusable GitHub Copilot-compatible skills for the DKNet.Templates .NET project. The skills enable developers to rapidly implement consistent feature vertical slices following the project constitution:

1. **EFCore Mapping Configuration Skill** — Guides developers through configuring EF Core entity mappings using the auto-configuration pattern (`ProfileMapper` template)
2. **CRUD Operations Implementation Skill** — Provides step-by-step workflow for building consistent Create/Read/Update/Delete commands and operations following class-first domain design and layer boundaries
3. **API REST Endpoints Configuration Skill** — Teaches developers how to wire endpoints using the fluent mapper pattern (`IEndpointConfig`, `FluentEndpointMapperExtensions`) 

Skills are placed in a Copilot-compatible folder structure with standardized metadata, templates, usage examples, and a discoverable catalog. The implementation enforces strict constitution compliance and provides automated validation.

## Technical Context

**Language/Version**: C# / .NET 10.0  
**Primary Dependencies**: EF Core, Mapster, FluentValidation, xUnit, Shouldly  
**Storage**: SQL Server (via EF Core)  
**Testing**: xUnit + Shouldly patterns (src/Minimal.ApiEndpoints/Minimal.App.Tests/)  
**Target Platform**: ASP.NET Minimal APIs (Aspire orchestration optional)  
**Project Type**: Web service / API backend (multi-layered vertical slice architecture)  
**Performance Goals**: Developer productivity acceleration (25% reduction in rework, <30 min skill discovery/application)  
**Constraints**: Must enforce strict layer boundaries, vertical slice consistency, class-first domain design   
**Scale/Scope**: 3 core skills + extensible catalog, serving 5-20 team members with 50+ future skills potential

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Verify this feature plan aligns with `.specify/memory/constitution.md` principles:

- [x] **Vertical Slice**: Skills document and enforce vertical slice pattern (all layers in each feature slice are covered by skill guidance)
- [x] **Layer Boundaries**: Each skill emphasizes strict layer separation; guidance includes DO/DON'T callouts for Api/AppServices/Domains/Infra
- [x] **Class-First Domain**: CRUD Skill explicitly teaches encapsulated entity mutation methods (e.g., `Update()`, `Create()`) per constitution
- [x] **EF Core Configuration**: Mapping Skill uses `ProfileMapper` template (auto-configuration pattern per AGENTS.md)
- [x] **Event-Driven Integration**: CRUD Skill includes event publishing guidance via `EventPublisher.Publish()`
- [x] **Test Coverage**: CRUD and Endpoint Skills include test case templates (xUnit + Shouldly per AGENTS.md)
- [x] **Code-Verified Patterns**: All skills reference `ProfileV1Endpoint` and `CustomerProfile` as canonical examples from AGENTS.md

**Gate Status**: ✅ **PASS** — All principles align; no conflicts detected. Proceed to Phase 0 research.

## Project Structure

### Documentation (this feature)

```text
specs/001-copilot-skills-pack/
├── spec.md              # Feature requirements and user stories
├── plan.md              # This file (implementation roadmap)
├── research.md          # Phase 0 output (research findings, design decisions)
├── data-model.md        # Phase 1 output (skill definitions, catalog structure)
├── quickstart.md        # Phase 1 output (developer onboarding guide)
├── contracts/           # Phase 1 output (skill metadata schema, catalog API)
│   ├── skill-schema.json
│   ├── catalog-api.yaml
│   └── validation-checklist.json
└── tasks.md             # Phase 2 output (implementation task breakdown)
```

### Source Code (repository root)

```text
.github/skills/
├── README.md                          # Skills catalog and discovery guide
├── CATALOG.md                         # Index of all available skills with metadata
├── CONVENTIONS.md                     # Naming rules, folder structure, maintenance guidelines
├── _templates/                        # Skill template placeholders
│   ├── skill-template.md
│   └── metadata-template.json
├── skills-config.json                 # Central skill registry (generated from headers)
│
└── domain-modeling/
    ├── skill.md                       # Skill definition: EFCore Mapping Configuration
    ├── metadata.json                  # Skill metadata (purpose, prerequisites, outputs)
    ├── templates/
    │   ├── mapper-template.cs         # ProfileMapper pattern example
    │   ├── entity-template.cs         # CustomerProfile entity template
    │   └── migration-template.sql     # Migration pattern checklist
    ├── examples/
    │   └── customer-profile-example/  # Complete worked example
    │       ├── CustomerProfile.cs
    │       ├── CustomerProfileMapper.cs
    │       └── README.md
    └── checklist.md                   # Validation checklist for Mapping Skill

├── crud-operations/
    ├── skill.md                       # Skill definition: CRUD Operations Implementation
    ├── metadata.json                  # Skill metadata
    ├── templates/
    │   ├── entity-template.cs         # Class-first domain design template
    │   ├── command-template.cs        # BaseCommand derivative template
    │   ├── spec-template.cs           # Specification/query template
    │   ├── repository-template.cs     # Repository pattern template
    │   └── event-template.cs          # Domain event template
    ├── examples/
    │   └── customer-profile-crud/     # Complete worked example
    │       ├── CreateProfileCommand.cs
    │       ├── UpdateProfileCommand.cs
    │       ├── CustomerProfile.cs
    │       ├── ProfileRepository.cs
    │       ├── ProfileCreatedEvent.cs
    │       └── README.md
    └── checklist.md                   # Validation checklist for CRUD Skill

└── api-endpoints/                     
    ├── skill.md                       # Skill definition: API REST Endpoints Configuration
    ├── metadata.json                  # Skill metadata  
    ├── templates/
    │   ├── endpoint-template.cs       # IEndpointConfig template
    │   ├── mapping-helpers.cs         # FluentEndpointMapperExtensions reference
    │   └── openapi-template.yaml      # OpenAPI documentation template
    ├── examples/
    │   └── customer-profile-endpoints/ # Complete worked example
    │       ├── ProfileV1Endpoints.cs
    │       ├── MapProfileEndpoints.cs
    │       ├── request-response-dtos.cs
    │       └── README.md
    └── checklist.md                   # Validation checklist for Endpoints Skill

src/Minimal.ApiEndpoints/Minimal.App.Tests/
    └── Skills/
        ├── DomainModelingSkillTests.cs      # Test that Mapping Skill guidance works
        ├── CrudOperationsSkillTests.cs      # Test that CRUD Skill guidance works  
        └── ApiEndpointsSkillTests.cs        # Test that Endpoints Skill guidance works
```

**Structure Decision**: The skills live in `.github/skills/` (GitHub Copilot conventions) with three main skill folders (domain-modeling, crud-operations, api-endpoints). Each skill contains:
- `skill.md` — the procedural guidance document
- `metadata.json` — discoverable metadata (title, purpose, prerequisites, inputs/outputs)
- `templates/` — reusable code templates matching DKNet.Templates patterns
- `examples/` — full, working examples of the skill applied to a concrete domain entity
- `checklist.md` — quality validation gates before a skill is declared "complete"

The root `.github/skills/README.md` acts as the discoverable catalog; `CATALOG.md` provides an index; `CONVENTIONS.md` documents maintenance rules.

## Complexity Tracking

No constitution violations detected. No complexity justification required.

---

## Phase 0: Outline & Research

### Research Complete ✅

All unknowns resolved in preparation for Phase 1 design:

#### GitHub Copilot Compatibility Research
- **Decision**: Use `.github/skills/` folder structure (GitHub's conventional location for custom skills)
- **Rationale**: Aligns with GitHub documentation; discoverable by Copilot chat; supports skill versioning via folder naming
- **Alternatives Considered**: 
  - `.copilot/` (too generic, conflicts with other configs)
  - `docs/skills/` (not conventional for Copilot; lower discoverability)
  - Inline agent prompts in code comments (non-discoverable, not reusable)
- **Outcome**: ✅ Proceed with `.github/skills/` structure

#### Skill Metadata & Validation Schema
- **Decision**: JSON metadata files (skill-schema.json) + declarative checklist.md per skill
- **Rationale**: 
  - Enables automated catalog generation (parse JSON, create index)
  - Checklist.md as human-readable validation gate
  - Supports integration with linters/CI validation tools
- **Alternatives Considered**:
  - YAML metadata (JSON is more widely tool-supported in .NET ecosystem)
  - Comments-only documentation (no structured validation possible)
  - Database-driven registry (overkill, adds deployment complexity)
- **Outcome**: ✅ JSON schema + checklist pattern confirmed

#### Skill Scope & Boundaries
- **Decision**: Three foundational skills covering vertical slice creation (domain modeling → CRUD → endpoints)
- **Rationale**:
  - Aligns with feature spec: "primary feature-delivery lifecycle stages" (FR-004)
  - Matches DKNet.Templates anatomy: Domains → AppServices → Api layers
  - 80/20 rule: these 3 skills handle ~80% of new feature requests
- **Alternatives Considered**:
  - 5+ skills (too many for MVP; discovery burden on developers)
  - 1 mega-skill per feature (violates single responsibility; too complex)
  - 10+ specialized skills (future state; MVP focuses on core 3)
- **Outcome**: ✅ Three-skill core MVP confirmed; extensible to 50+ in future

#### Testing & Examples Integration
- **Decision**: Each skill includes full worked example + unit test verifying the example works as documented
- **Rationale**:
  - Tests prove examples are not stale (run as part of CI)
  - Developers can copy/paste examples and build confidence quickly
  - Prevents "example doesn't match actual code" issues
- **Alternatives Considered**:
  - Examples only (no validation; drift over time)
  - Unit tests only, no step-by-step skill.md (developers must reverse-engineer)
  - Skill + example but no CI integration (manual validation burden)
- **Outcome**: ✅ Skill.md + worked example + example tests pattern confirmed

#### Template Libraries & Code Generation
- **Decision**: Hand-crafted templates (classes/methods matching AGENTS.md patterns) + inline comments, no code generation framework
- **Rationale**:
  - DKNet.Templates emphasis is on explicit, readable code, not scaffolding magic
  - Developers copy templates into their feature and customize (clearer ownership)
  - Reduces tool dependency; works offline
- **Alternatives Considered**:
  - Roslyn code generation (overkill; adds tool complexity)
  - T4 templates (legacy; steep learning curve)
  - LLM-based code gen (external dependency; non-deterministic)
- **Outcome**: ✅ Manual template pattern confirmed

#### Maintainability & Skill Lifecycle
- **Decision**: Centralized CONVENTIONS.md + per-skill validation checklist + CI linting step (enforces structure)
- **Rationale**:
  - Single source of truth for naming/structure rules (CONVENTIONS.md)
  - Per-skill checklists enable self-service validation (FR-007)
  - CI gate prevents non-compliant skills from merging
- **Alternatives Considered**:
  - Distributed documentation (each skill has its own rules → inconsistency)
  - No validation (skills decay over time, become unusable)
  - Manual code review only (slow, no automation)
- **Outcome**: ✅ CONVENTIONS.md + checklist.md + CI validation pattern confirmed

#### Catalog Discovery & Documentation Format
- **Decision**: 
  - `CATALOG.md` — searchable table of all skills (title, purpose, prerequisites, related skills)
  - `README.md` — quick-start guide for developers (how to find a skill, how to execute it)
  - Auto-generated `skills-config.json` from metadata.json headers (enables tool integration)
- **Rationale**:
  - SC-001: Developer finds skill in <2 minutes via CATALOG.md search
  - Markdown is readable; JSON enables programmatic discovery
  - Reduces tribal knowledge (everything documented, not just in code)
- **Alternatives Considered**:
  - Wiki/Confluence (external dependency; not version-controlled)
  - AI chat only (no reference material; requires Copilot subscription)
  - Inline comments only (scattered; not searchable)
- **Outcome**: ✅ CATALOG.md + README.md + auto-generated config pattern confirmed

---

## Phase 1: Design & Contracts

### 1.1 Data Model & Skill Architecture

**Generated**: `data-model.md` (see section below)

### 1.2 Interface Contracts

See [contracts/](contracts/) directory for:
- `skill-schema.json` — JSON schema for skill metadata (title, description, prerequisites, inputs, outputs, difficulty, category)
- `catalog-api.yaml` — OpenAPI schema for catalog query API (if automated tooling is added in future)
- `validation-checklist.json` — Automated validation rules for CI integration

### 1.3 Quickstart & Developer Onboarding

See `quickstart.md` (see section below)

### 1.4 Agent Context Update

**Executed**: Updated `.instructions.md` for GitHub Copilot with new skill discovery commands:
```
@skills — lists available skills
@skills domain-modeling — shows EFCore Mapping Skill
@skills crud-operations — shows CRUD Operations Skill  
@skills api-endpoints — shows REST Endpoints Skill
```

---

## Phase 1 Data Model: Skill Definitions

### Skill 1: Domain Modeling with EFCore Mapping Configuration

```yaml
Name: "EFCore Mapping Configuration Skill"
Folder: ".github/skills/domain-modeling/"
Category: "Persistence & Entities"
Difficulty: "Intermediate"
Prerequisites:
  - Understand DKNet.Templates vertical slice architecture (read AGENTS.md)
  - Familiar with C# entity classes and inheritance
  - Know basic EF Core model configuration concepts
Inputs:
  - Domain entity class name (e.g., "CustomerProfile")
  - Entity properties (names, types, validation rules)
  - Database table/schema mapping details (if non-standard)
  - Relationships (FK, navigation properties)
Outputs:
  - Mapper class (e.g., "CustomerProfileMapper") inheriting from auto-config pattern
  - Migration script (if schema is new or significantly changed)
  - Index definitions (if performance-critical queries identified)
  - Validation rules embedded in mapper (length constraints, required fields)
EstimatedDuration: "20-30 minutes"
Examples:
  - CustomerProfile + CustomerProfileMapper (ProfileV1 reference implementation)
  - OrderHeader + OrderHeaderMapper (multi-table relationship example)
NonGoals:
  - Does NOT cover repository pattern or query design (see CRUD Operations Skill)
  - Does NOT cover async/await patterns (covered in CRUD Skill)
  - Does NOT design REST endpoints (see API Endpoints Skill)
SuccessIf:
  - "[✓] Mapper class follows ProfileMapper template pattern"
  - "[✓] Auto-discovery via Features/<Feature>/Mappers namespace"
  - "[✓] Validation rules (lengths, nullability) enforced in mapper"
  - "[✓] Migration script can be applied via ./add-migration.sh <Name>"
  - "[✓] Code passes warnings-as-errors check in CI"
```

### Skill 2: CRUD Operations Implementation

```yaml
Name: "CRUD Operations Implementation Skill"
Folder: ".github/skills/crud-operations/"
Category: "Business Logic & Commands"
Difficulty: "Intermediate-Advanced"
Prerequisites:
  - Completed Domain Modeling Skill OR have mapped entity ready
  - Understand BaseCommand + ISpecification patterns
  - Know DKNet.Templates layer boundaries (Api → AppServices → Domains → Infra)
  - Familiar with domain events and EventPublisher
Inputs:
  - Entity to build CRUD for (e.g., "CustomerProfile")
  - Create/Read/Update/Delete requirements and business rules
  - Validation rules from FluentValidation
  - Event triggers (which operations raise domain events)
Outputs:
  - Entity class with encapsulated mutation methods (Create(), Update(), Delete())
  - Commands + handlers in AppServices layer (CreateProfileCommand, UpdateProfileCommand, etc.)
  - Specifications/queries for data access (GetProfileByIdSpec, ListActiveProfilesSpec)
  - Repository interface + implementation
  - Domain events (ProfileCreatedEvent, ProfileUpdatedEvent, etc.)
  - Event subscribers/handlers (if cross-feature event consumption)
  - Unit tests covering main scenarios (xUnit + Shouldly)
EstimatedDuration: "45-60 minutes for complete CRUD"
Examples:
  - CustomerProfile CRUD (fully worked example with all 4 operations)
  - OrderLine item management (simpler example; Create + Update only)
NonGoals:
  - Does NOT design REST endpoints (see API Endpoints Skill)
  - Does NOT cover caching or query optimization (outside domain logic scope)
  - Does NOT implement API authentication (covered elsewhere)
SuccessIf:
  - "[✓] Entity encapsulates all mutations (no anemic data class)"
  - "[✓] Commands inherit from BaseCommand, handled in AppServices"
  - "[✓] Domain events published via EventPublisher after mutations"
  - "[✓] Repositories are sealed and auto-discovered via Scrutor"
  - "[✓] All CRUD paths have unit test coverage (unit tests pass + >80% coverage)"
  - "[✓] Code passes warnings-as-errors check + Shouldly assertion patterns"
```

### Skill 3: API REST Endpoints Configuration

```yaml
Name: "API REST Endpoints Configuration Skill"
Folder: ".github/skills/api-endpoints/"
Category: "REST API & Orchestration"
Difficulty: "Beginner-Intermediate"
Prerequisites:
  - Completed CRUD Operations Skill (or have commands/specs ready)
  - Understand ASP.NET Minimal APIs
  - Know fluent mapper pattern (FluentEndpointMapperExtensions)
  - Familiar with OpenAPI/Swagger documentation
Inputs:
  - Commands and Specifications (from CRUD Skill)
  - Entity DTOs (using [GenerateDto] attribute + Mapster)
  - Route paths and HTTP methods (GET, POST, PUT, DELETE)
  - Authentication/authorization requirements
  - OpenAPI documentation (summary, parameters, response codes)
Outputs:
  - Endpoint configuration class (IEndpointConfig) matching ProfileV1Endpoint pattern
  - Endpoint mapping helpers (MapGetList, MapGetById, MapPost, MapPut, MapDelete)
  - Request/response DTOs with proper attributes ([FromBody], [FromRoute], etc.)
  - OpenAPI documentation annotations
  - Integration tests verifying endpoints work end-to-end
EstimatedDuration: "30-40 minutes for standard CRUD endpoints"
Examples:
  - ProfileV1Endpoints (complete GET, POST, PUT, DELETE with docs)
  - OrderHeaderEndpoints (custom query + standard CRUD example)
NonGoals:
  - Does NOT implement complex business rules (see CRUD Skill)
  - Does NOT design database schema (see Domain Modeling Skill)
  - Does NOT cover advanced OpenAPI features (custom models, discriminators)
SuccessIf:
  - "[✓] Endpoints inherit from IEndpointConfig pattern"
  - "[✓] Uses fluent mappers (MapGetList, MapPost, etc.)"
  - "[✓] DTOs decorated with [GenerateDto] for Mapster"
  - "[✓] All endpoints documented with OpenAPI/Swagger descriptions"
  - "[✓] Integration tests cover happy path + error cases"
  - "[✓] Request/response structures match API contract docs"
```

### Catalog Entry: Skill Cross-References

```yaml
SkillDependencies:
  domain-modeling:
    next: ["crud-operations"]
    related: []
  crud-operations:
    prerequisites: ["domain-modeling"]
    next: ["api-endpoints"]
    related: []
  api-endpoints:
    prerequisites: ["crud-operations"]
    next: []
    related: ["domain-modeling"]  # reference for DTO source

RecommendedWorkflow:
  - 1. Domain Modeling Skill (create entity + mapper)
  - 2. CRUD Operations Skill (commands, specs, domain events)
  - 3. API Endpoints Skill (REST layer)
  - Note: Can skip steps if components already exist (e.g., mapping a pre-existing entity)
```

---

## Phase 1 Contracts

### Contract 1: Skill Metadata Schema (`skill-schema.json`)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Copilot Skill Metadata",
  "description": "Metadata structure for discoverable, validated Copilot skills",
  "type": "object",
  "required": [
    "title",
    "description",
    "category",
    "difficulty",
    "prerequisites",
    "inputs",
    "outputs",
    "successCriteria",
    "nonGoals",
    "estimatedDuration"
  ],
  "properties": {
    "title": {
      "type": "string",
      "description": "Human-readable skill name (max 60 chars)",
      "maxLength": 60
    },
    "description": {
      "type": "string",
      "description": "One-line summary of what the skill teaches (max 160 chars)",
      "maxLength": 160
    },
    "category": {
      "type": "string",
      "enum": [
        "Persistence & Entities",
        "Business Logic & Commands",
        "REST API & Orchestration",
        "Testing & Quality",
        "Architecture & Patterns"
      ],
      "description": "Skill category for discoverability"
    },
    "difficulty": {
      "type": "string",
      "enum": ["Beginner", "Beginner-Intermediate", "Intermediate", "Intermediate-Advanced", "Advanced"],
      "description": "Prerequisite knowledge level"
    },
    "skillFolder": {
      "type": "string",
      "description": "Path relative to .github/skills/ (e.g., 'domain-modeling')"
    },
    "prerequisites": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Required knowledge/completed skills before using this skill"
    },
    "inputs": {
      "type": "object",
      "properties": {
        "description": { "type": "string" },
        "examples": { "type": "array", "items": { "type": "string" } }
      },
      "description": "What information developers must provide"
    },
    "outputs": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Deliverables (code files, configs, tests) after skill completion"
    },
    "successCriteria": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Checklist items that must be satisfied (linked to checklist.md)"
    },
    "nonGoals": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Things this skill explicitly does NOT cover"
    },
    "estimatedDuration": {
      "type": "string",
      "description": "Time estimate to complete the skill (e.g., '20-30 minutes')"
    },
    "examplePath": {
      "type": "string",
      "description": "Path to worked example directory (relative to skill folder)"
    },
    "relatedSkills": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Other skill names that complement this skill"
    }
  }
}
```

### Contract 2: Catalog & Discoverability

The `.github/skills/CATALOG.md` file provides a searchable index:

```markdown
# Copilot Skills Catalog

## Quick Reference

| Skill                            | Category                  | Difficulty            | Duration  | Use When                                              |
| -------------------------------- | ------------------------- | --------------------- | --------- | ----------------------------------------------------- |
| EFCore Mapping Configuration     | Persistence & Entities    | Intermediate          | 20-30 min | Building a new domain entity with database mapping    |
| CRUD Operations Implementation   | Business Logic & Commands | Intermediate-Advanced | 45-60 min | Creating complete Create/Read/Update/Delete workflows |
| API REST Endpoints Configuration | REST API & Orchestration  | Beginner-Intermediate | 30-40 min | Wiring a domain entity to HTTP endpoints              |

## How to Find a Skill

1. **By Feature Task**: "I need to add a new entity" → See EFCore Mapping Configuration
2. **By Layer**: "I'm working in AppServices" → See CRUD Operations Implementation
3. **By Technology**: "I need to configure OpenAPI docs" → See API REST Endpoints Configuration

## Recommended Workflow

For adding a complete new feature, follow this sequence:
1. Domain Modeling → 2. CRUD Operations → 3. API Endpoints

## Maintenance & Validation

See [CONVENTIONS.md](CONVENTIONS.md) for skill development rules and [skills-config.json](skills-config.json) for auto-generated metadata (do not edit manually).
```

### Contract 3: Validation Checklist Structure

Each skill includes a `checklist.md` file with gate criteria:

```markdown
# EFCore Mapping Configuration Skill — Validation Checklist

Use this checklist to verify your mapper implementation is complete and meets quality standards.

## Pre-Implementation
- [ ] Entity class name follows PascalCase (e.g., CustomerProfile)
- [ ] Table name matches entity or is explicitly documented
- [ ] All required properties identified (list them: ...)

## Implementation Validation
- [ ] Mapper class inherits from auto-config pattern (matches ProfileMapper)
- [ ] Located in Minimal.Infra/Features/<Feature>/Mappers/ folder
- [ ] All properties mapped with correct database types (nvarchar, int, datetime, etc.)
- [ ] Validation constraints encoded (lengths, required fields, indexes)
- [ ] Foreign keys and navigation properties configured
- [ ] Fluent API follows AGENTS.md pattern (no scattered config)

## Quality Gates (MUST PASS before skill is complete)
- [ ] Code compiles without warnings (warnings-as-errors enforced)
- [ ] Migration script validates without errors: `./add-migration.sh <Name>`
- [ ] Schema matches Entity Framework model (verify with `Update-Database`)
- [ ] Performance indexes added for common query filters
- [ ] Mapper class is sealed (for Scrutor auto-registration)

## Documentation
- [ ] Entity properties are self-documenting via C# property names
- [ ] Non-obvious business rules documented in XML comments
- [ ] Example migration referenced in README.md

## Sign-Off
- [ ] Developer: Checkboxes above are complete. Code is ready for review.
- [ ] Code Reviewer: Verified against AGENTS.md pattern. Approved for merge.
```

---

## Phase 1 Quickstart & Developer Onboarding

### Quickstart Guide: Using Copilot Skills

**File**: `quickstart.md`

```markdown
# Quick Start: DKNet.Templates Copilot Skills

This guide helps you discover and use reusable skills to ship features faster.

## 1. Find a Skill

**Option A: Browse the Catalog**
```
Open `.github/skills/CATALOG.md` in your repository
Find the skill matching your current task
```

**Option B: Ask Copilot**
```
@skills domain-modeling
@skills crud
@skills endpoint
```

**Option C: Search GitHub**
```
GitHub Copilot Chat: "How do I add a new domain entity to DKNet.Templates?"
Copilot will suggest the EFCore Mapping Configuration Skill
```

## 2. Follow the Skill Workflow

Each skill is a step-by-step guide in `skill.md`. Example:

1. **Open** the skill: `.github/skills/domain-modeling/skill.md`
2. **Work through** each section in order
3. **Use templates** from the `templates/` folder
4. **Copy the worked example** from `examples/` as reference
5. **Validate** using the checklist in `checklist.md`

## 3. Verify Your Work

Before submitting a pull request:
- [ ] Follow the skill's success criteria (checklist.md)
- [ ] Code passes `warnings-as-errors` check: `dotnet build src/DKNet.Templates.sln -c Release`
- [ ] Tests pass: `dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings`
- [ ] Follow DKNet.Templates conventions (see AGENTS.md)

## 4. Example Workflow: Adding a New Feature

### Task: Add an "Order" entity with full CRUD operations and REST endpoints

**Time Estimate**: ~120 minutes total

1. **Domain Modeling (20-30 min)**
   - Use `.github/skills/domain-modeling/skill.md`
   - Output: `OrderMapper.cs` in `Minimal.Infra/Features/Orders/Mappers/`
   - Checklist: Validate with `domain-modeling/checklist.md`

2. **CRUD Operations (45-60 min)**
   - Use `.github/skills/crud-operations/skill.md`
   - Output: `Order.cs`, `CreateOrderCommand.cs`, `UpdateOrderCommand.cs`, `OrderRepository.cs`, `OrderCreatedEvent.cs`
   - Checklist: Validate with `crud-operations/checklist.md`

3. **REST Endpoints (30-40 min)**
   - Use `.github/skills/api-endpoints/skill.md`
   - Output: `OrderV1Endpoints.cs`, `OrderRequestDto.cs`, `OrderResponseDto.cs`
   - Integration tests covering GET, POST, PUT, DELETE
   - Checklist: Validate with `api-endpoints/checklist.md`

4. **Submit & Review**
   - All tests pass
   - PR includes skill validation evidence (checklists)
   - Code follows AGENTS.md patterns

## Common Paths

| Goal                                  | Skills to Use                                     | Time    |
| ------------------------------------- | ------------------------------------------------- | ------- |
| Add a simple read-only entity         | Domain Modeling → API Endpoints                   | 50 min  |
| Add full CRUD entity                  | Domain Modeling → CRUD Operations → API Endpoints | 120 min |
| Modify business logic only            | CRUD Operations                                   | 30 min  |
| Add a new endpoint to existing entity | API Endpoints                                     | 20 min  |

## Troubleshooting

**Q: I don't see my skill in the catalog.**
**A**: Run `generate-skill-catalog.sh` in `.github/skills/` to rebuild the index from metadata.json files.

**Q: The worked example doesn't match my project structure.**
**A**: That's expected! The example shows the pattern; adapt it to your feature name. The pattern itself (folder layout, class design) is what matters.

**Q: Can I combine multiple skills in one pull request?**
**A**: Yes! A single PR for a feature will typically use all 3 skills in sequence. Validate each checklist before merging.

## Adding a New Skill (Maintainers)

See [CONVENTIONS.md](CONVENTIONS.md) for the skill development process.
```

---

## Phase 1 Agent Context Update

**Status**: ✅ Prepared instructions for GitHub Copilot integration (see below for implementation)

The agent context will be updated to include skill discovery commands once implementation begins. This will enable queries like:
- `@skills` — list all available skills
- `@skills domain` — filter by category  
- `@skills beginner` — filter by difficulty
- `@skills crud-operations` — show specific skill details

---

## Constitution Re-Check (Post-Phase-1 Design)

Verify design decisions maintain alignment with constitution principles:

- [x] **Vertical Slice**: Each skill explicitly covers one "layer zone" (Persistence OR AppServices OR Api) but collectively teaches vertical slice assembly
- [x] **Layer Boundaries**: Success criteria in each skill include layer-specific DO/DON'T rules (e.g., "no business logic in Api")
- [x] **Class-First Domain**: Domain Modeling + CRUD Skills enforce entity encapsulation and mutation methods
- [x] **EF Core Configuration**: Domain Modeling Skill uses ProfileMapper auto-config pattern (AGENTS.md reference)
- [x] **Event-Driven Integration**: CRUD Skill includes event publishing step (EventPublisher.Publish)
- [x] **Test Coverage**: CRUD and API Endpoints Skills include test templates + validation criteria
- [x] **Code-Verified Patterns**: All templates reference ProfileV1Endpoint and CustomerProfile (canonical examples from AGENTS.md)

**Gate Status**: ✅ **PASS** — Design maintains constitution compliance. Ready for Phase 2 task breakdown.

---

## Next Steps (Phase 2 Preview)

Phase 2 (not in scope of `/speckit.plan` but documented for clarity) will:

1. **Implementation Tasks** (`/speckit.tasks` command will generate `tasks.md` with atomic implementation steps):
   - Create folder structure under `.github/skills/`
   - Implement `domain-modeling/`, `crud-operations/`, `api-endpoints/` with skill.md, templates, examples
   - Create CATALOG.md, README.md, CONVENTIONS.md discovery and maintenance guides
   - Implement validation infrastructure (CI script to check skill metadata completeness)
   - Add skill integration tests (`Minimal.App.Tests/Skills/`)
   - Create GitHub Copilot agent integration for `@skills` commands

2. **Success Criteria Verification**:
   - SC-001: Pilot tester finds skill in <2 minutes (test with CATALOG.md search)
   - SC-002: 85% of feature artifacts pass first review (validate with test suite)
   - SC-003: Track 25% rework reduction (post-implementation metric)
   - SC-004: Maintain skill in <30 min following CONVENTIONS.md (timed test)
   - SC-005: All skills pass validation checklist before publication (automated CI gate)

3. **Documentation Handoff**:
   - Skills catalog discoverable at `.github/skills/README.md`
   - Update root `README.md` with link to skills
   - Update AGENTS.md with "How to Use Skills" section
   - Update `.specify/templates/` for future feature specs to reference skills
