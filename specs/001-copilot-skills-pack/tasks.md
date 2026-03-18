# Phase 2 Implementation Tasks: Reusable Copilot Skills Pack

**Feature**: 001-copilot-skills-pack  
**Date**: 2026-03-17  
**Status**: Implementation Ready  
**Input**: spec.md, plan.md, data-model.md, research.md  

---

## Overview & Scope

**Phase 2 Goal**: Build the complete foundational infrastructure for the skills pack and implement three core skills with templates, examples, validation, and discovery mechanisms.

**MVP Deliverables**:
- ✅ Foundational infrastructure (folder structure, conventions, validation schema)
- ✅ Three complete skills (Domain Modeling, CRUD Operations, API Endpoints)
- ✅ Catalog and discovery system (README, CATALOG.md, automated indexing)
- ✅ Skill validation and CI integration
- ✅ Developer onboarding guide

**Not in MVP** (Phase 3+):
- Advanced skills (testing strategies, caching, versioning, state machines)
- User interface / web-based skill browser
- GitHub Copilot Chat integration (@copilot prompts)
- Automated code generation from templates

---

## Dependency Graph

```
FOUNDATIONAL INFRASTRUCTURE (must complete first)
├── T001: Setup .github/copilot/skills/ folder structure
├── T002: Create CONVENTIONS.md for skill development standards
├── T003: Create skill-schema.json validation schema
└── T004: Create base template files and validation checklist template

SKILL 1: Domain Modeling (builds on foundational infrastructure)
├── T005: [P] Create Domain Modeling Skill core documentation
├── T006: [P] Create entity and mapper templates
├── T007: [P] Create CustomerProfile working example
├── T008: [P] Create validation checklist for Domain Modeling Skill
└── T009: Create unit tests for Domain Modeling Skill templates

SKILL 2: CRUD Operations (builds on Skill 1 artifacts)
├── T010: [P] Create CRUD Operations Skill core documentation
├── T011: [P] Create command, spec, repository, and event templates
├── T012: [P] Create complete CustomerProfile CRUD workflow example
├── T013: [P] Create validation checklist for CRUD Operations Skill
└── T014: Create unit tests for CRUD Operations Skill templates

SKILL 3: API Endpoints (builds on Skill 2 artifacts)
├── T015: [P] Create API Endpoints Skill core documentation
├── T016: [P] Create endpoint, DTO, and OpenAPI templates
├── T017: [P] Create complete ProfileV1Endpoints working example
├── T018: [P] Create validation checklist for API Endpoints Skill
└── T019: Create integration tests for API Endpoints Skill templates

CATALOG & DISCOVERY (builds on all three skills)
├── T020: [P] Create CATALOG.md index with skill search
├── T021: [P] Create root README.md with quick-start guide
├── T022: Generate metadata.json files for all three skills
└── T023: [P] Implement automated catalog generation and validation

TESTING & VALIDATION (independent, can run in parallel with skills after T004)
├── T024: [P] Create skill metadata validation unit tests
├── T025: [P] Create skill guidance execution tests (xUnit + Shouldly)
└── T026: End-to-end test: developer uses all 3 skills for new feature

DOCUMENTATION & TRAINING (final phase, after all skills complete)
├── T027: Create developer onboarding guide (30-min walkthrough)
├── T028: Create skill migration guide for existing code
├── T029: Create troubleshooting FAQ for skill usage
└── T030: Create maintenance runbook for skill lifecycle

PARALLEL EXECUTION OPPORTUNITIES:
- Skills 1, 2, 3 can develop templates in parallel after T004 (T005-T008, T010-T013, T015-T018)
- Catalog/discovery work can begin after all skills are documented (T020-T023)
- Testing can start early (T024) using test-driven approach
- Final documentation (T027-T030) runs in parallel with late skill work
```

---

## Phase 2 Implementation Tasks

### PHASE 2A: Foundational Infrastructure (Blocking Prerequisites)

**Goal**: Establish the folder structure, conventions, and validation mechanisms that all skills depend on.  
**Effort**: 3-4 hours  
**Owner**: Lead architect/Skills maintainer  
**Completion Criteria**: All foundational files created, validation schema defined, CI hooks registered.

---

#### Task Setup & Folder Structure

- [ ] **T001** Create `.github/skills/` folder structure and base files
  - **File Path**: `.github/skills/`
  - **Description**: Create root directory with `_templates/`, `domain-modeling/`, `crud-operations/`, `api-endpoints/` subfolders. Each skill folder shall contain `templates/`, `examples/`, and reserved slots for `skill.md`, `metadata.json`, `checklist.md`.
  - **Acceptance Criteria**:
    - [✓] Folder structure matches project plan exactly
    - [✓] `.gitkeep` files placed in empty example folders so structure commits cleanly
    - [✓] All folders committed to git (not ignored)
  - **Estimated Effort**: 15 min
  - **Dependencies**: None

- [ ] **T002** Create `CONVENTIONS.md` — skill development standards and maintenance rules
  - **File Path**: `.github/skills/CONVENTIONS.md`
  - **Description**: Document naming conventions, folder structure rules, metadata schema requirements, mandatory file checklist, and the skill lifecycle (draft → validation → published → deprecated). Include examples of correct vs. incorrect skill structure.
  - **Content Sections**:
    - Folder naming convention (kebab-case, e.g., `domain-modeling`)
    - File naming convention (skill.md, metadata.json, checklist.md are mandatory; examples/ folder optional but recommended)
    - Mandatory fields in metadata.json (id, title, category, prerequisites, inputs, outputs, successCriteria, nonGoals, examples path, tests path)
    - Skill status lifecycle and approval gates
    - Versioning rules (semantic versioning for skill changes)
    - Deprecation process (announce, maintain for 2 releases, remove)
  - **Acceptance Criteria**:
    - [✓] All conventions map to Phase 1 data-model definitions
    - [✓] Examples of correct/incorrect structure provided
    - [✓] Maintenance rules clear enough for a mid-level developer to add a new skill
  - **Estimated Effort**: 30 min
  - **Dependencies**: T001

- [ ] **T003** Create `skill-schema.json` validation schema (JSON Schema draft-07)
  - **File Path**: `.github/skills/_templates/skill-schema.json`
  - **Description**: Define JSON Schema that validates the structure of `metadata.json` for any skill. Schema shall enforce: required fields (id, title, category, prerequisites, inputs, outputs, successCriteria, nonGoals, examples path, testPath), type constraints, and relationships.
  - **Schema Properties**:
    - `id` (string, kebab-case): Unique identifier
    - `title` (string): Human-readable skill name
    - `category` (enum): One of ["Persistence & Entities", "Business Logic & Commands", "REST API & Orchestration"]
    - `difficulty` (enum): One of ["Beginner", "Intermediate", "Advanced"]
    - `estimatedDurationMinutes` (object): { min: number, max: number }
    - `prerequisites` (array): List of prerequisites (strings)
    - `inputs` (object): { description: string, items: [strings] }
    - `outputs` (object): { description: string, items: [strings] }
    - `successCriteria` (array): Checklist items
    - `nonGoals` (array): Out-of-scope items
    - `folderPath` (string): Path to skill folder
    - `relatedSkills` (array): IDs of related skills
    - `testPath` (string): Path to test file
  - **Acceptance Criteria**:
    - [✓] Schema validates against JSON Schema draft-07 specification
    - [✓] All three Phase 1 skill definitions pass validation against this schema
    - [✓] Schema can be checked in CI (e.g., via `ajv-cli` or custom script)
  - **Estimated Effort**: 45 min
  - **Dependencies**: T001, T002

- [ ] **T004** Create `_templates/` base templates for skill developers
  - **File Path**: `.github/copilot/skills/_templates/`
  - **Description**: Create reusable templates for skill authors to copy and customize: `skill-template.md`, `metadata-template.json`, `checklist-template.md`. Each shall include inline comments explaining sections and expectations.
  - **Templates**:
    - `skill-template.md`: Markdown structure with sections (Overview, Prerequisites, Step-by-Step Guide, Validation Checklist, Examples, Common Errors)
    - `metadata-template.json`: Example metadata.json with all required fields + helpful comments
    - `checklist-template.md`: Reusable validation checklist format with checkbox format
  - **Acceptance Criteria**:
    - [✓] Templates include clear copy-to-skill instructions
    - [✓] Inline comments guide skill authors on what to fill in
    - [✓] All three Phase 1 skills can be created by following these templates
    - [✓] Templates are version-controlled and not excluded from git
  - **Estimated Effort**: 45 min
  - **Dependencies**: T002, T003

---

### PHASE 2B: Skill 1 – Domain Modeling with EFCore Mapping Configuration

**Goal**: Enable developers to consistently create domain entities and EF Core persistence mappings using the auto-configuration pattern.  
**Estimated Duration**: 20–30 min per developer; 4–5 hours to complete skill (guides + examples + tests).  
**Owner**: Any developer familiar with EF Core and ProfileMapper pattern  
**Completion Criteria**: Skill.md complete, templates provided, working example committed, validation tests passing.

---

#### Skill 1 Documentation & Guidance

- [ ] **T005** [P] Create `domain-modeling/skill.md` — Core skill documentation for EFCore mapping
  - **File Path**: `.github/copilot/skills/domain-modeling/skill.md`
  - **Description**: Write step-by-step procedural guide for creating a domain entity and its EF Core mapper. Include: context (when to use), prerequisites, inputs, step-by-step workflow, validation checklist, examples, and common errors.
  - **Content Sections**:
    - **Overview**: When to use this skill (adding a new entity to the database)
    - **Prerequisites**: Before you start (read AGENTS.md Architecture, understand C# classes)
    - **Inputs Checklist**: What information you need (entity name, properties, relationships, validation rules)
    - **Step-by-Step Workflow**:
      - Step 1: Define the entity class in Minimal.Domains/Features/<Feature>/Entities/
      - Step 2: Implement mutation methods (Create(), Update(), Delete() if applicable)
      - Step 3: Create mapper class in Minimal.Infra/Features/<Feature>/Mappers/ inheriting from auto-config base
      - Step 4: Configure properties (ConfigureProperty for types, lengths, required fields)
      - Step 5: Configure relationships (HasOne, HasMany, WithMany)
      - Step 6: Configure indexes for query performance
      - Step 7: Create migration script (./add-migration.sh <MigrationName>)
      - Step 8: Verify schema (run migration, check database)
    - **Success Validation**: Link to checklist.md
    - **Common Errors & Fixes**: "Mapper class not auto-discovered" → "Must be in Mappers/ folder, must be sealed", etc.
    - **Related Skills**: CRUD Operations Skill (next logical step)
  - **Template Source**: Use skill-template.md structure from T004
  - **Acceptance Criteria**:
    - [✓] Skill.md maps exactly to data-model.md Skill 1 definition
    - [✓] Step-by-step workflow is independently followable by a mid-level C# developer
    - [✓] All prerequisites and inputs are listed (no tribal knowledge assumed)
    - [✓] Validation checklist referenced (not embedded; linked to checklist.md)
    - [✓] Examples include ProfileMapper pattern from AGENTS.md
  - **Estimated Effort**: 90 min
  - **Dependencies**: T001, T002, T004

- [ ] **T006** [P] Create `domain-modeling/templates/` — Entity and mapper template files
  - **File Path**: `.github/copilot/skills/domain-modeling/templates/`
  - **Description**: Provide copy-paste-ready C# templates for entity class and mapper class. Templates shall include inline comments, TODO markers, and example property definitions for developers to adapt.
  - **Templates**:
    - `entity-template.cs`: Minimal entity class with constructor, property definitions, and Create() method stub; TODO markers for properties specific to the domain
    - `mapper-template.cs`: Mapper class inheriting from auto-config base, with ConfigureProperty and HasOne examples; sealed keyword enforced; sealed on class and methods as per project standards
    - `migration-template.sql`: (Optional) SQL migration checklist comments for developers reviewing generated migrations
  - **Content Quality**:
    - [✓] Templates include clear TODO and CHANGEME markers
    - [✓] Inline comments explain EF Core configuration options
    - [✓] Sealed keyword and auto-discovery pattern shown correctly
    - [✓] Examples match ProfileMapper and CustomerProfile patterns from AGENTS.md
  - **Acceptance Criteria**:
    - [✓] Templates can be copied and customized in <10 min
    - [✓] Sealed keyword on all classes (Scrutor auto-registration requirement)
    - [✓] ConfigureProperty calls shown for common types (string, int, DateTime, decimal)
    - [✓] Navigation properties configured (HasOne, WithMany) if relationships needed
  - **Estimated Effort**: 60 min
  - **Dependencies**: T001, T005

#### Skill 1 Examples & Validation

- [ ] **T007** [P] Create `domain-modeling/examples/customer-profile-example/` — Working CustomerProfile entity + mapper
  - **File Path**: `.github/copilot/skills/domain-modeling/examples/customer-profile-example/`
  - **Description**: Provide complete, production-ready example of the Domain Modeling Skill applied to CustomerProfile entity. Include: entity class, mapper, and README explaining each part.
  - **Files**:
    - `CustomerProfile.cs`: Full entity class with:
      - Properties: Id (Guid), UserId (Guid), FullName (string), Email (string), DateOfBirth (DateTime?), CreatedAt (DateTime), UpdatedAt (DateTime)
      - Constructor with entity creation logic
      - Create() static method for factory pattern
      - Update() method for encapsulated mutations
      - All properties with descriptive comments
    - `CustomerProfileMapper.cs`: Auto-config mapper inheriting from base, with:
      - ConfigureProperty calls for each field (type, length, required/optional)
      - Validation rules (email max length 256, full name max 200)
      - HasOne relationship to User (if applicable)
      - Composite indexes (UserId + Email for query optimization)
      - Full inline comments explaining each configuration line
    - `README.md`: Explanation of the example, mapping of each property, and how to customize for a different entity
  - **Code Quality**:
    - [✓] Follows all patterns from AGENTS.md ProfileMapper section
    - [✓] Sealed classes throughout
    - [✓] All comments present and explanatory
    - [✓] No warnings (warnings-as-errors enforced)
  - **Acceptance Criteria**:
    - [✓] Example compiles without errors or warnings
    - [✓] Mapper follows auto-config pattern exactly (sealed, in correct namespace)
    - [✓] Property configurations cover all common EF Core scenarios
    - [✓] README explains every configuration choice and how to adapt for different entities
  - **Estimated Effort**: 90 min
  - **Dependencies**: T004, T006

- [ ] **T008** [P] Create `domain-modeling/checklist.md` — Validation checklist for Domain Modeling Skill
  - **File Path**: `.github/copilot/skills/domain-modeling/checklist.md`
  - **Description**: Quality gate checklist that developers must complete before their entity + mapper is considered "done." Checklist items map to success criteria from data-model.md Skill 1 definition.
  - **Checklist Sections**:
    - **Entity Class**: 
      - [✓] Mapper class follows ProfileMapper template pattern from AGENTS.md
      - [✓] Class is placed in `Minimal.Infra/Features/<Feature>/Mappers/` (auto-discovery)
      - [✓] Mapper class is sealed (for Scrutor auto-registration)
    - **Property Mapping**:
      - [✓] All entity properties correctly mapped to database types
      - [✓] Validation rules (lengths, nullability) enforced in ConfigureProperty
      - [✓] Foreign keys and navigation properties configured
    - **Migration & Schema**:
      - [✓] Migration script applies without errors: `./add-migration.sh <MigrationName>`
      - [✓] Schema matches Entity Framework model (run and verify with Update-Database)
      - [✓] Performance indexes added for common query filters (if applicable)
    - **Code Quality**:
      - [✓] Code compiles without warnings (warnings-as-errors enforced in CI)
      - [✓] XML documentation comments added for non-obvious configuration
  - **Acceptance Criteria**:
    - [✓] Checklist has 10+ items covering all success criteria from data-model.md
    - [✓] Checkbox format (- [x] Item) for easy copy-paste tracking
    - [✓] Links to example and templates provided
    - [✓] Failed checklist item → clear remediation guidance
  - **Estimated Effort**: 45 min
  - **Dependencies**: T002, T007

---

#### Skill 1 Tests

- [ ] **T009** Create `src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/DomainModelingSkillTests.cs`
  - **File Path**: `src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/DomainModelingSkillTests.cs`
  - **Description**: Unit tests verifying that Domain Modeling Skill templates and guidance produce valid code artifacts. Tests shall verify: mapper auto-discovery, property configuration correctness, sealed class requirement, and migration generation.
  - **Test Cases** (xUnit + Shouldly):
    - **Test 1**: "Mapper class in Mappers/ folder is auto-discovered by Scrutor"
      - Arrange: Create test mapper in correct location
      - Act: Run DI container startup
      - Assert: Mapper is registered and resolvable
    - **Test 2**: "Property configuration with ConfigureProperty produces correct EF model"
      - Arrange: Load CustomerProfileMapper
      - Act: Reflect on EF model for configured properties
      - Assert: Property type, length, and nullability match configuration
    - **Test 3**: "Foreign key relationships configured with HasOne are valid"
      - Arrange: Load mapper with HasOne relationship
      - Act: Validate relationship navigation properties exist
      - Assert: Relationship is correctly bound (no navigation property null)
    - **Test 4**: "Sealed class requirement enforced (fails if mapper not sealed)"
      - Arrange: Create test case with non-sealed mapper
      - Act: Attempt registration
      - Assert: Fails with clear error message
    - **Test 5**: "Migration script generated by add-migration.sh is syntactically valid"
      - Arrange: Trigger migration generation
      - Act: Parse migration file (basic syntax check)
      - Assert: File contains recognizable EF migration code (MigrationBuilder calls)
  - **Code Quality**:
    - [✓] Tests use xUnit + Shouldly patterns from AGENTS.md
    - [✓] Test names are descriptive (should follow "Mapper_ConfiguresProperty_WithCorrectType" pattern or similar)
    - [✓] Assertions use Shouldly fluent syntax (.Should().Be(), .Should().NotBeNull())
    - [✓] Tests are isolated (no cross-test dependencies)
  - **Acceptance Criteria**:
    - [✓] All 5+ test cases pass with CustomerProfileMapper example
    - [✓] Test file compiles without warnings
    - [✓] Tests can be run independently with `dotnet test`
    - [✓] Code coverage ≥80% for mapper configuration logic
  - **Estimated Effort**: 120 min
  - **Dependencies**: T007, T008

---

### PHASE 2C: Skill 2 – CRUD Operations Implementation

**Goal**: Guide developers through building consistent Create/Read/Update/Delete workflows with encapsulated domain mutations, commands, and events.  
**Estimated Duration**: 45–60 min per developer; 6–8 hours to complete skill.  
**Owner**: Developer experienced with BaseCommand pattern and domain events.  
**Completion Criteria**: Skill.md complete, templates for all CRUD components, CustomerProfile CRUD example, tests passing.

---

#### Skill 2 Documentation & Guidance

- [ ] **T010** [P] Create `crud-operations/skill.md` — Core skill documentation for CRUD workflows
  - **File Path**: `.github/copilot/skills/crud-operations/skill.md`
  - **Description**: Write comprehensive step-by-step guide for implementing Create, Read, Update, Delete operations. Include: context, prerequisites, inputs, multi-step workflow across all layers, validation checklist links, and detailed examples.
  - **Content Sections**:
    - **Overview**: When to use this skill (implementing business logic for entity mutations)
    - **Prerequisites**: Before you start (completed Domain Modeling Skill, understand BaseCommand and layer boundaries)
    - **Inputs Checklist**: What you need (entity class, mutation requirements, business rules, event triggers)
    - **Step-by-Step Workflow**:
      - Step 1: Design entity mutation methods (Create(), Update(), Delete()) in domain entity
      - Step 2: Implement BaseCommand + CommandHandler for each operation (AppServices layer)
      - Step 3: Define ISpecification queries for Read operations (AppServices or Domains layer)
      - Step 4: Implement Repository interface (Domains) + sealed implementation (Infra)
      - Step 5: Define domain events (ProfileCreatedEvent, ProfileUpdatedEvent, etc.)
      - Step 6: Wire event publishing via EventPublisher.Publish()
      - Step 7: (Optional) Create event subscribers/handlers
      - Step 8: Write unit tests for entity mutations and commands
      - Step 9: (Optional) Write integration tests for end-to-end workflow
      - Step 10: Validate against checklist
    - **Layer-by-Layer Guidance**:
      - **Domains layer**: Entity class with encapsulated methods, events, repository interfaces
      - **AppServices layer**: Commands, command handlers, specifications/queries
      - **Infra layer**: Repository implementation, event publishers (if custom)
    - **Validation & Testing**: Link to checklist.md and test patterns
    - **Common Errors & Fixes**
    - **Related Skills**: Domain Modeling (prerequisite), API Endpoints (next step)
  - **Template Source**: Use skill-template.md structure
  - **Acceptance Criteria**:
    - [✓] Skill.md maps exactly to data-model.md Skill 2 definition
    - [✓] Multi-step workflow covers all four CRUD operations (Create, Read, Update, Delete)
    - [✓] Layer-specific guidance ensures developers don't mix concerns
    - [✓] Examples reference BaseCommand, ISpecification, and EventPublisher from AGENTS.md
    - [✓] All prerequisites clearly stated (no tribal knowledge)
  - **Estimated Effort**: 120 min
  - **Dependencies**: T001, T002, T004

- [ ] **T011** [P] Create `crud-operations/templates/` — Command, spec, repository, and event templates
  - **File Path**: `.github/copilot/skills/crud-operations/templates/`
  - **Description**: Provide copy-paste templates for all CRUD components: command class (Create/Update), specification, repository interface + implementation, and domain event. Include inline comments and TODO markers.
  - **Templates**:
    - `command-template.cs`: BaseCommand-derived class with validation properties and handler skeleton; TODO markers for command-specific fields
    - `spec-template.cs`: ISpecification-derived class for querying; example for "GetByIdSpec" pattern
    - `repository-template.cs`: Interface + sealed implementation; constructor injection, sealed keyword, Scrutor pattern
    - `entity-methods-template.cs`: Entity mutation methods (Create() factory, Update() method, Delete() method) with encapsulation patterns
    - `event-template.cs`: Domain event class inheriting from base event type
  - **Code Quality**:
    - [✓] All classes follow project pattern (sealed where required, correct namespaces)
    - [✓] Inline comments explain each pattern element
    - [✓] TODO markers guide developers on what to customize
    - [✓] Validation patterns shown (FluentValidation attributes, custom validators)
  - **Acceptance Criteria**:
    - [✓] Templates can be adapted in 15–20 min per template
    - [✓] Sealed keyword enforced on all classes
    - [✓] BaseCommand and ISpecification patterns correctly shown
    - [✓] Repository interface/implementation separation clear
  - **Estimated Effort**: 90 min
  - **Dependencies**: T001, T010

#### Skill 2 Examples & Validation

- [ ] **T012** [P] Create `crud-operations/examples/customer-profile-crud/` — Complete CustomerProfile CRUD example
  - **File Path**: `.github/copilot/skills/crud-operations/examples/customer-profile-crud/`
  - **Description**: Provide full production-ready example of complete CRUD workflow for CustomerProfile entity. Include: entity mutation methods, Create/Update commands + handlers, specifications, repository, events, tests, and README.
  - **Files**:
    - `CustomerProfile.cs` (entity with mutations):
      - Create() static factory method
      - Update() method with encapsulated business logic
      - Delete() or Archive() method (if applicable)
      - All methods raise domain events
    - `CreateProfileCommand.cs` + `CreateProfileCommandHandler.cs`:
      - Command inheriting from BaseCommand with validation
      - Handler implementing Create logic, repository add, event publish
    - `UpdateProfileCommand.cs` + `UpdateProfileCommandHandler.cs`:
      - Update command with change fields (FullName, Email, etc.)
      - Handler fetching existing, calling entity.Update(), repository save
    - `GetProfileByIdSpec.cs`:
      - ISpecification for querying profile by ID
      - Includes any includes/navigation properties needed
    - `ProfileRepository.cs`:
      - Interface in Domains layer
      - Sealed implementation in Infra layer
      - Methods: Add(), Update(), Delete(), Get(), GetMany()
    - `ProfileCreatedEvent.cs` + `ProfileUpdatedEvent.cs`:
      - Domain events with relevant context (ProfileId, ChangesSummary, etc.)
    - `ProfileCreatedEventConsumer.cs` (example event handler):
      - Example of a subscriber listening to ProfileCreatedEvent
      - Demonstrates inter-feature communication
    - `CustomerProfileTests.cs`:
      - xUnit + Shouldly tests for entity mutations and commands
      - Happy path + error scenarios
      - Test data builders (if using Bogus or similar)
    - `README.md`:
      - Walks through each component
      - Maps to CRUD Operations Skill steps
      - Explains customization points for a different entity
  - **Code Quality**:
    - [✓] All patterns from AGENTS.md followed exactly
    - [✓] Sealed classes, correct namespaces
    - [✓] No warnings (warnings-as-errors enforced)
    - [✓] Tests are comprehensive and follow xUnit + Shouldly
  - **Acceptance Criteria**:
    - [✓] Example compiles and runs without errors
    - [✓] Tests pass with >80% coverage of entity+ command logic
    - [✓] README explains each file and how to adapt for different entity
    - [✓] Event publishing wired correctly via EventPublisher.Publish()
  - **Estimated Effort**: 150 min
  - **Dependencies**: T006, T010, T011

- [ ] **T013** [P] Create `crud-operations/checklist.md` — Validation checklist for CRUD Operations Skill
  - **File Path**: `.github/copilot/skills/crud-operations/checklist.md`
  - **Description**: Quality gate checklist for CRUD implementation. Maps to success criteria in data-model.md Skill 2.
  - **Checklist Sections**:
    - **Domain Entity**:
      - [✓] Entity encapsulates all mutations (Create(), Update(), Delete() methods)
      - [✓] Domain properties have no public setters (encapsulation enforced)
      - [✓] Entity raises domain events on mutations (no event publishing in AppServices)
    - **Commands & Handlers**:
      - [✓] Commands inherit from BaseCommand
      - [✓] Handlers live in AppServices layer
      - [✓] Validation rules enforced at command level (FluentValidation)
      - [✓] Handlers use repository to persist changes
    - **Specifications & Queries**:
      - [✓] Specifications implement ISpecification pattern
      - [✓] Queries in AppServices + Domains layer, not in Api layer
    - **Repository**:
      - [✓] Repository interface defined in Domains layer
      - [✓] Implementation is sealed and in Infra/Features/<Feature>/Repos/
      - [✓] Scrutor auto-discovery: sealed class in correct namespace
    - **Events & Publishing**:
      - [✓] Domain events defined (ProfileCreatedEvent, etc.)
      - [✓] Events published via EventPublisher.Publish() after mutations
      - [✓] (Optional) Event subscribers defined if cross-feature consumed
    - **Testing**:
      - [✓] Unit tests for entity mutations (Create(), Update(), Delete())
      - [✓] Unit tests for commands and handlers
      - [✓] Tests use xUnit + Shouldly patterns
      - [✓] Test coverage ≥80% for domain + command logic
    - **Code Quality**:
      - [✓] Code compiles without warnings
      - [✓] Sealed classes where required
      - [✓] Correct namespaces and layer placement
  - **Acceptance Criteria**:
    - [✓] Checklist has 15+ items
    - [✓] Each failed item links to remediation guidance
    - [✓] Checkbox format for easy tracking
  - **Estimated Effort**: 60 min
  - **Dependencies**: T002, T012

---

#### Skill 2 Tests

- [ ] **T014** Create `src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/CrudOperationsSkillTests.cs`
  - **File Path**: `src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/CrudOperationsSkillTests.cs`
  - **Description**: Unit tests verifying that CRUD Operations Skill templates produce valid artifacts and workflows. Test: command handling, domain mutation encapsulation, event publishing, repository persistence, and specification queries.
  - **Test Cases** (xUnit + Shouldly):
    - **Test 1**: "CreateProfileCommand handler creates entity, publishes event, and persists"
      - Arrange: Create command with valid profile data
      - Act: Handle command (mock repository)
      - Assert: Repository.Add() called, ProfileCreatedEvent published, handler returns success
    - **Test 2**: "Entity.Create() factory creates entity with correct initial state"
      - Arrange: Profile data
      - Act: Call CustomerProfile.Create(...)
      - Assert: Entity properties set, no domain events yet (lazy raise)
    - **Test 3**: "Entity.Update() raises ProfileUpdatedEvent"
      - Arrange: Existing CustomerProfile entity
      - Act: Call entity.Update(newName, newEmail)
      - Assert: Entity properties changed, ProfileUpdatedEvent raised (accessible via DomainEvents)
    - **Test 4**: "Repository sealed and auto-discovered by DI container"
      - Arrange: Build service provider with InfraSetup wiring
      - Act: Resolve IProfileRepository from DI
      - Assert: Concrete implementation resolved, not inline mock
    - **Test 5**: "Specification queries filter correctly (GetByIdSpec)"
      - Arrange: Database with multiple profiles; GetByIdSpec for specific ID
      - Act: Execute specification against DbSet
      - Assert: Only matching profile returned
    - **Test 6**: "Validation rule violations caught at command level (FluentValidation)"
      - Arrange: Command with invalid data (e.g., email too long)
      - Act: Validate command
      - Assert: Validation fails with descriptive error message
  - **Code Quality**:
    - [✓] xUnit + Shouldly patterns
    - [✓] Descriptive test names
    - [✓] Isolated tests (no cross-test dependencies)
    - [✓] Mocks for external dependencies (repository, message bus)
  - **Acceptance Criteria**:
    - [✓] All 6+ test cases pass
    - [✓] Test file compiles without warnings
    - [✓] Tests can be run independently
    - [✓] Coverage ≥80% for command handlers and domain entity
  - **Estimated Effort**: 120 min
  - **Dependencies**: T012, T013

---

### PHASE 2D: Skill 3 – API REST Endpoints Configuration

**Goal**: Help developers wire domain commands and queries to HTTP endpoints with fluent mappers, DTOs, and OpenAPI documentation.  
**Estimated Duration**: 30–40 min per developer; 4–5 hours to complete skill.  
**Owner**: Developer familiar with Minimal APIs and IEndpointConfig pattern.  
**Completion Criteria**: Skill.md, templates, ProfileV1Endpoints example, integration tests.

---

#### Skill 3 Documentation & Guidance

- [ ] **T015** [P] Create `api-endpoints/skill.md` — Core skill documentation for Endpoints
  - **File Path**: `.github/copilot/skills/api-endpoints/skill.md`
  - **Description**: Step-by-step guide for wiring commands/queries to HTTP endpoints. Include: context, prerequisites, inputs, workflow, validation checklist, and examples.
  - **Content Sections**:
    - **Overview**: When to use this skill (exposing business logic via REST API)
    - **Prerequisites**: Before you start (completed CRUD Operations Skill, understand Minimal APIs, know fluent mappers)
    - **Inputs Checklist**: Commands, specifications, DTOs, route definitions
    - **Step-by-Step Workflow**:
      - Step 1: Define request/response DTOs with [GenerateDto] attribute
      - Step 2: Create endpoint config class implementing IEndpointConfig
      - Step 3: Wire commands/queries in endpoint methods
      - Step 4: Use fluent mappers (MapPost, MapPut, MapDelete, MapGetList, MapGetById)
      - Step 5: Add OpenAPI documentation (summary, response types, parameters)
      - Step 6: Handle validation errors (return 400 BadRequest)
      - Step 7: Handle not-found cases (return 404 NotFound)
      - Step 8: Write integration tests
      - Step 9: Verify endpoint with curl or Swagger UI
    - **Fluent Mapper Pattern Guide**: Explain MapPost syntax, how it wires request DTO → command → handler → response DTO
    - **OpenAPI/Swagger Documentation**: Add ProduceResponseType, WithOpenApi(), etc.
    - **Testing Guidance**: Integration test patterns for REST endpoints
    - **Common Errors**: Wrong DTO binding, missing validation, incorrect status codes
    - **Related Skills**: CRUD Operations (prerequisite), Domain Modeling (transitive dependency)
  - **Template Source**: Use skill-template.md structure
  - **Acceptance Criteria**:
    - [✓] Skill.md maps to data-model.md Skill 3 definition
    - [✓] Workflow is followable by mid-level ASP.NET developer
    - [✓] Examples reference ProfileV1Endpoint from AGENTS.md
    - [✓] OpenAPI documentation section detailed and practical
  - **Estimated Effort**: 90 min
  - **Dependencies**: T001, T002, T004

- [ ] **T016** [P] Create `api-endpoints/templates/` — Endpoint, DTO, and OpenAPI templates
  - **File Path**: `.github/copilot/skills/api-endpoints/templates/`
  - **Description**: Provide copy-paste templates for HTTP endpoints, DTOs, and OpenAPI documentation. Include: IEndpointConfig class, request/response DTOs with [GenerateDto], MapPost/Put/Get syntax, and OpenAPI decorators.
  - **Templates**:
    - `endpoint-template.cs`: IEndpointConfig-derived class with Map() method skeleton; fluent mapper examples (MapPost, MapPut, etc.)
    - `dto-template.cs`: Request and response DTO classes with [GenerateDto] attributes; TODO markers for properties
    - `openapi-template.yaml`: OpenAPI specification snippet showing endpoint documentation (tags, summary, parameters, responses)
    - `fluent-mappers-reference.cs`: Reference guide showing syntax for MapPost, MapPut, MapDelete, MapGetList, MapGetById (non-runnable; documentation only)
  - **Code Quality**:
    - [✓] All templates follow IEndpointConfig pattern from AGENTS.md
    - [✓] Sealed classes where required
    - [✓] Mapster [GenerateDto] shown correctly
    - [✓] OpenAPI decorations with ProduceResponseType, WithOpenApi(), etc.
  - **Acceptance Criteria**:
    - [✓] Templates can be customized in 10–15 min
    - [✓] Fluent mapper syntax is clear and production-ready
    - [✓] OpenAPI syntax matches Minimal APIs conventions
  - **Estimated Effort**: 60 min
  - **Dependencies**: T001, T015

#### Skill 3 Examples & Validation

- [ ] **T017** [P] Create `api-endpoints/examples/customer-profile-endpoints/` — ProfileV1Endpoints full example
  - **File Path**: `.github/copilot/skills/api-endpoints/examples/customer-profile-endpoints/`
  - **Description**: Complete production-ready example of REST endpoints for CustomerProfile. Include: endpoint config, DTOs, integration tests, and README.
  - **Files**:
    - `ProfileV1Endpoints.cs`:
      - Implements IEndpointConfig
      - Methods: GetList (MapGetList), GetById (MapGetById), Create (MapPost), Update (MapPut), Delete (MapDelete)
      - Fluent mappers wired to commands/specs
      - OpenAPI documentation with ProduceResponseType, WithOpenApi()
      - Validation error handling (return 400 with error messages)
      - Not-found handling (return 404)
    - `ProfileRequestDto.cs`:
      - CreateProfileRequestDto with [GenerateDto(...)]
      - UpdateProfileRequestDto with changeable fields
      - Mapster generates DTO→Command mapping
    - `ProfileResponseDto.cs`:
      - ProfileResponseDto with [GenerateDto(...)]
      - Maps entity→response DTO
    - `MapProfileEndpoints.cs` (alternative: optional helper if using extension method):
      - Extension method on IEndpointRouteBuilder to register ProfileV1Endpoints
      - Called from Program.cs
    - `ProfileEndpointsTests.cs`:
      - Integration tests using HttpClient
      - Tests each endpoint: GET list, GET by ID, POST create, PUT update, DELETE
      - Validates HTTP status codes, response structure, error scenarios
      - Uses WebApplicationFactory pattern (if applicable in this project)
    - `README.md`:
      - Explanation of each endpoint
      - curl command examples for testing
      - Maps to API Endpoints Skill steps
      - Customization guidance for different entity
  - **Code Quality**:
    - [✓] Follows ProfileV1Endpoint pattern from AGENTS.md exactly
    - [✓] Sealed classes, correct namespaces
    - [✓] No warnings (warnings-as-errors)
    - [✓] Integration tests comprehensive (happy path + error scenarios)
  - **Acceptance Criteria**:
    - [✓] Example compiles and runs without errors
    - [✓] All endpoints testable via curl or Swagger UI
    - [✓] Integration tests pass with >70% coverage of endpoint logic
    - [✓] README explains customization for different entity
  - **Estimated Effort**: 120 min
  - **Dependencies**: T012, T015, T016

- [ ] **T018** [P] Create `api-endpoints/checklist.md` — Validation checklist for API Endpoints Skill
  - **File Path**: `.github/copilot/skills/api-endpoints/checklist.md`
  - **Description**: Quality gate checklist for endpoint implementation. Maps to success criteria in data-model.md Skill 3.
  - **Checklist Sections**:
    - **Endpoint Configuration**:
      - [✓] Endpoints implement IEndpointConfig pattern
      - [✓] Uses fluent mappers (MapGetList, MapGetById, MapPost, MapPut, MapDelete)
      - [✓] Sealed class (if applicable)
    - **DTOs**:
      - [✓] Request/response DTOs decorated with [GenerateDto(...)]
      - [✓] Mapster auto-generates DTO implementations
      - [✓] DTOs have proper attributes ([FromBody], [FromRoute], etc.)
    - **API Contract & Routing**:
      - [✓] Route paths follow REST conventions (/api/v1/profiles, /api/v1/profiles/{id})
      - [✓] HTTP methods match semantics (GET for read, POST for create, PUT for update, DELETE for delete)
      - [✓] Request/response structures documented
    - **OpenAPI Documentation**:
      - [✓] All endpoints documented with OpenAPI/Swagger descriptions
      - [✓] ProduceResponseType attributes added (200, 201, 400, 404, 500)
      - [✓] Parameters documented ([FromRoute], [FromBody])
      - [✓] Response models linked to DTOs
    - **Error Handling**:
      - [✓] Validation errors return 400 BadRequest with descriptive messages
      - [✓] Not-found cases return 404 NotFound
      - [✓] Unexpected errors return 500 InternalServerError
      - [✓] Error response structure consistent across endpoints
    - **Testing**:
      - [✓] Integration tests cover happy path
      - [✓] Integration tests cover error scenarios (validation, not found, unauthorized)
      - [✓] Tests use xUnit + assertion library from project
    - **Code Quality**:
      - [✓] Code compiles without warnings
      - [✓] Sealed classes where required
      - [✓] Correct namespaces and layer placement
  - **Acceptance Criteria**:
    - [✓] Checklist has 15+ items
    - [✓] Each failed item links to remediation (e.g., "Missing OpenAPI docs?" → "Add ProduceResponseType and .WithOpenApi()")
    - [✓] Checkbox format
  - **Estimated Effort**: 45 min
  - **Dependencies**: T002, T017

---

#### Skill 3 Tests

- [ ] **T019** Create `src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/ApiEndpointsSkillTests.cs`
  - **File Path**: `src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/ApiEndpointsSkillTests.cs`
  - **Description**: Integration tests verifying that API Endpoints Skill templates work end-to-end. Test: endpoint routing, command wiring, DTO mapping, error handling, and OpenAPI generation.
  - **Test Cases** (xUnit):
    - **Test 1**: "POST /api/v1/profiles creates profile and returns 201 Created"
      - Arrange: Create HTTP POST request with ProfileRequestDto
      - Act: Send request to endpoint
      - Assert: Response status 201, body contains created profile
    - **Test 2**: "GET /api/v1/profiles/{id} returns profile with 200 OK"
      - Arrange: Create profile in database; construct GET request with ID
      - Act: Send request
      - Assert: Response status 200, body matches profile data
    - **Test 3**: "GET /api/v1/profiles/{id} returns 404 NotFound for non-existent ID"
      - Arrange: Generate non-existent profile ID
      - Act: Send GET request
      - Assert: Response status 404
    - **Test 4**: "POST /api/v1/profiles with invalid email returns 400 BadRequest"
      - Arrange: Create request with invalid email
      - Act: Send POST request
      - Assert: Response status 400, error message mentions email validation
    - **Test 5**: "PUT /api/v1/profiles/{id} updates profile and returns 200 OK"
      - Arrange: Create profile; prepare update request
      - Act: Send PUT request
      - Assert: Response status 200, profile data updated in database
    - **Test 6**: "DELETE /api/v1/profiles/{id} deletes profile and returns 204 NoContent"
      - Arrange: Create profile; construct DELETE request
      - Act: Send DELETE request
      - Assert: Response status 204, profile no longer in database
    - **Test 7**: "OpenAPI schema includes all endpoints and proper documentation"
      - Arrange: Request OpenAPI spec from /swagger (or /openapi.json)
      - Act: Load OpenAPI document
      - Assert: All profile endpoints present, have descriptions and response types
  - **Code Quality**:
    - [✓] xUnit test patterns
    - [✓] Uses HttpClient (or WebApplicationFactory) to make real HTTP calls
    - [✓] Clear Arrange-Act-Assert structure
    - [✓] Isolated tests (clean database state between tests)
  - **Acceptance Criteria**:
    - [✓] All 7+ test cases pass
    - [✓] Test file compiles without warnings
    - [✓] Tests can be run independently
    - [✓] Coverage ≥70% for endpoint logic (routing, command dispatch, DTO mapping)
  - **Estimated Effort**: 120 min
  - **Dependencies**: T017, T018

---

### PHASE 2E: Catalog & Discovery Infrastructure

**Goal**: Build discoverable catalog and automated indexing so developers can easily find and use skills.  
**Effort**: 3–4 hours  
**Completion Criteria**: CATALOG.md, README.md, metadata.json generation, CI validation hooks.

---

#### Catalog & Discovery Building

- [ ] **T020** [P] Create `.github/copilot/skills/CATALOG.md` — Master index of all skills
  - **File Path**: `.github/copilot/skills/CATALOG.md`
  - **Description**: Human-readable index and discovery guide for all available skills. Include: skill cards (title, purpose, difficulty, duration), recommended workflows, and search hints.
  - **Content Structure**:
    - **Quick Start**: "How to find the right skill for your task" (flowchart or decision tree)
    - **Skill Cards** (one per skill):
      - Title, category, difficulty level
      - Purpose: one-sentence description of when to use
      - Duration: estimated time to complete
      - Prerequisites: what you need before starting
      - Outputs: what you'll have after completing
      - Related skills: links to complementary skills
      - Link to full skill.md and examples
    - **Workflow Recommendations**:
      - "New feature from scratch" → Skill 1 → Skill 2 → Skill 3
      - "Update existing entity" → Skill 2 → Skill 3 (skip Skill 1)
      - "Add REST endpoints only" → Skill 3 (if entity + commands already exist)
    - **Search Tips**: "How to search this index for your task"
    - **Maintenance Notes**: "How to add new skills to this catalog (see CONVENTIONS.md)"
  - **Acceptance Criteria**:
    - [✓] CATALOG.md is discoverable (linked from README.md, visible in file tree)
    - [✓] Skill cards include all required metadata (title, purpose, duration, prerequisites, outputs)
    - [✓] Workflows show clear dependency chain
    - [✓] Links are relative paths (work offline)
    - [✓] Markdown format is clean and scannable
  - **Estimated Effort**: 60 min
  - **Dependencies**: T005, T010, T015

- [ ] **T021** [P] Create `.github/copilot/skills/README.md` — Root discovery guide & quick start
  - **File Path**: `.github/copilot/skills/README.md`
  - **Description**: Main entry point for developers discovering the skills pack. Include: brief overview, quick-start walkthrough, link to full CATALOG.md, and maintenance instructions.
  - **Content Sections**:
    - **What Are These Skills?**: One-paragraph overview (reusable, team-oriented, GitHub Copilot-compatible)
    - **Quick Start Path**: "Get started in 5 minutes"
      - Step 1: Read overview in CATALOG.md
      - Step 2: Pick a skill matching your task
      - Step 3: Open that skill's skill.md
      - Step 4: Follow step-by-step workflow
      - Step 5: Check your work against the checklist
    - **Skill Categories** (with brief descriptions):
      - Persistence & Entities → Domain Modeling Skill
      - Business Logic & Commands → CRUD Operations Skill
      - REST API & Orchestration → API Endpoints Skill
    - **Full Catalog**: Link to CATALOG.md for searchable index
    - **For Maintainers**: Link to CONVENTIONS.md for adding new skills
    - **Questions or Issues**: Link to project contributing guide (or support channel)
  - **Acceptance Criteria**:
    - [✓] README.md is the landing page for `.github/copilot/skills/` folder
    - [✓] Quick-start path is under 2 minutes to read
    - [✓] All navigation links present and working
    - [✓] Tone is welcoming and non-intimidating (encouraging developers to use skills)
  - **Estimated Effort**: 30 min
  - **Dependencies**: T020

- [ ] **T022** Generate `metadata.json` files for all three skills with validated content
  - **File Path**: `.github/copilot/skills/domain-modeling/metadata.json`, `.github/copilot/skills/crud-operations/metadata.json`, `.github/copilot/skills/api-endpoints/metadata.json`
  - **Description**: Create canonical metadata.json for each skill (domain-modeling, crud-operations, api-endpoints). Each shall contain all required fields per skill-schema.json, sourced from corresponding skill.md and data-model.md sections.
  - **Metadata Content** (per skill; example for domain-modeling):
    ```json
    {
      "id": "dknet-domain-modeling",
      "title": "EFCore Mapping Configuration Skill",
      "category": "Persistence & Entities",
      "difficulty": "Intermediate",
      "estimatedDurationMinutes": { "min": 20, "max": 30 },
      "prerequisites": [
        "Read AGENTS.md for architecture overview",
        "Familiar with C# classes and inheritance",
        "Know basic EF Core model configuration"
      ],
      "inputs": {
        "description": "Information provided by developer before starting skill",
        "items": [
          "Domain entity class name (e.g., CustomerProfile)",
          "Entity properties (field names, C# types)",
          "Validation rules (field lengths, required/optional)",
          "Relationships (foreign keys, navigation properties)"
        ]
      },
      "outputs": {
        "description": "Deliverables after completing skill",
        "items": [
          "Mapper class inheriting from auto-config base",
          "EF Core migration script (if new table or schema change)",
          "Index definitions for performance-critical paths",
          "Validation constraints encoded in mapping"
        ]
      },
      "successCriteria": [
        "[✓] Mapper class follows ProfileMapper template pattern",
        "[✓] Class placed in Minimal.Infra/Features/<Feature>/Mappers/",
        "[✓] Sealed class for Scrutor auto-registration",
        "[✓] All entity properties correctly mapped",
        "[✓] Validation rules enforced in ConfigureProperty",
        "[✓] Migration script applies without errors"
      ],
      "nonGoals": [
        "Does NOT cover repository pattern (see CRUD Operations Skill)",
        "Does NOT cover queries/specifications",
        "Does NOT design REST endpoints (see API Endpoints Skill)"
      ],
      "folderPath": ".github/copilot/skills/domain-modeling/",
      "skillFile": "skill.md",
      "examplePath": "examples/customer-profile-example/",
      "checklistFile": "checklist.md",
      "testPath": "src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/DomainModelingSkillTests.cs",
      "relatedSkills": ["crud-operations"],
      "nextSkill": "crud-operations"
    }
    ```
  - **Generation Method**:
    - [ ] Manually fill each metadata.json based on skill.md + data-model.md content
    - [ ] Validate each JSON against skill-schema.json using JSON schema validator (e.g., ajv-cli in CI)
    - [ ] Commit all three metadata.json files in single commit
  - **Acceptance Criteria**:
    - [✓] All three metadata.json files created and committed
    - [✓] Each validates cleanly against skill-schema.json
    - [✓] Metadata matches skill.md and data-model.md content (no discrepancies)
    - [✓] All required fields present (per schema)
  - **Estimated Effort**: 45 min
  - **Dependencies**: T003, T005, T010, T015

- [ ] **T023** [P] Implement automated catalog generation and CI validation
  - **File Path**: `.github/workflows/skills-validation.yml` (GitHub Actions workflow) + `.github/copilot/scripts/validate-skills.sh` (validation script)
  - **Description**: Create CI/CD pipeline to automatically validate and generated updated CATALOG.md. Pipeline shall: validate all metadata.json against schema, check folder structure compliance, ensure no duplicate skill IDs, and optionally regenerate CATALOG.md from metadata registry.
  - **Implementation Options** (pick one):
    - **Option A (Lightweight)**: Bash script + GitHub Actions workflow
      - Script checks: metadata.json existence, schema validation (ajv), folder structure
      - Workflow runs on PR: fails if validation fails
      - Generates updated CATALOG.md (optional; can be manual for now in MVP)
    - **Option B (Robust)**: .NET tool + GitHub Actions workflow
      - Custom .NET tool reads all metadata.json files, validates, generates CATALOG.md
      - Workflow runs on PR with dotnet tool invocation
  - **Scope for MVP**: Option A (lightweight bash + Actions)
  - **Workflow Configuration** (`.github/workflows/skills-validation.yml`):
    - Trigger: On push/PR to `.github/copilot/skills/`
    - Steps:
      1. Checkout code
      2. Run `.github/copilot/scripts/validate-skills.sh`
      3. Fail if validation errors detected
      4. (Optional) Generate CATALOG.md and commit changes (or fail if CATALOG.md out of date)
  - **Validation Script** (`.github/copilot/scripts/validate-skills.sh`):
    - For each skill folder (`domain-modeling`, `crud-operations`, `api-endpoints`):
      - Check that `skill.md`, `metadata.json`, `checklist.md` exist
      - Validate `metadata.json` against `skill-schema.json` using `ajv-cli` (or equivalent)
      - Check that skill ID is unique across all skills
      - Check that folder structure matches CONVENTIONS.md
    - Report errors with line numbers and remediation hints
  - **Acceptance Criteria**:
    - [✓] Workflow runs on every PR touching `.github/copilot/skills/`
    - [✓] Workflow fails if any metadata.json is invalid
    - [✓] Workflow fails if required files are missing
    - [✓] Workflow fails if skill structure violates CONVENTIONS.md
    - [✓] Error messages are clear and actionable (guide developers to fix)
  - **Estimated Effort**: 90 min
  - **Dependencies**: T002, T003, T022

---

### PHASE 2F: Testing & Validation (Cross-Cutting)

**Goal**: Provide comprehensive test coverage for skill validity, guideline adherence, and developer usability.  
**Effort**: 4–5 hours  
**Parallelizable**: Tests can start after T004 (templates available); no dependency on individual skill completion.  
**Completion Criteria**: All test suites passing, coverage gates met, E2E test passing.

---

#### Testing Infrastructure

- [ ] **T024** [P] Create skill metadata validation unit tests
  - **File Path**: `src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/SkillMetadataValidationTests.cs`
  - **Description**: Unit tests verifying that all skill metadata.json files conform to schema and contain required fields. Tests shall: load metadata, validate against schema, check field completeness, and verify relationships between skills.
  - **Test Cases** (xUnit):
    - **Test 1**: "All skill metadata.json files exist and are valid JSON"
      - Arrange: Load all metadata.json files
      - Act: Parse JSON
      - Assert: No parsing errors; all files exist
    - **Test 2**: "All metadata.json files validate against skill-schema.json"
      - Arrange: Load schema and metadata files
      - Act: Validate each metadata against schema
      - Assert: All pass validation (no schema violations)
    - **Test 3**: "All required fields present in metadata.json"
      - Arrange: Load metadata
      - Act: Check for: id, title, category, difficulty, prerequisites, inputs, outputs, successCriteria, nonGoals, folderPath, skillFile, testPath
      - Assert: All fields present and non-empty
    - **Test 4**: "Skill IDs are unique (no duplicates)"
      - Arrange: Load all skill metadata
      - Act: Collect skill IDs
      - Assert: Each ID appears exactly once
    - **Test 5**: "Related skills references are valid (IDs exist)"
      - Arrange: Load all metadata
      - Act: For each relatedSkills array, verify referenced IDs exist
      - Assert: No references to non-existent skills
    - **Test 6**: "Folder structure matches CONVENTIONS.md (skill.md, metadata.json, checklist.md exist)"
      - Arrange: List all skill folders
      - Act: Check file existence in each
      - Assert: All required files present in each skill folder
  - **Code Quality**:
    - [✓] xUnit test patterns
    - [✓] Descriptive test names
    - [✓] Assertions use Shouldly fluent style
  - **Acceptance Criteria**:
    - [✓] All 6+ test cases pass for existing three skills
    - [✓] Test file compiles without warnings
    - [✓] Can be run independently with `dotnet test`
  - **Estimated Effort**: 60 min
  - **Dependencies**: T003, T022

- [ ] **T025** [P] Create skill guidance execution tests (verify template usage patterns)
  - **File Path**: `src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/SkillGuidanceTests.cs`
  - **Description**: Integration-style tests verifying that following skill guidance produces correct codebase artifacts. Tests shall: instantiate examples from skill guidance, verify compilation, check for warnings, and validate that outputs match expected patterns.
  - **Test Cases** (xUnit):
    - **Test 1**: "Domain Modeling Skill example (CustomerProfileMapper) compiles without warnings"
      - Arrange: Load example 'profile-example/ mapper
      - Act: Compile project context containing the mapper
      - Assert: No compilation errors or warnings
    - **Test 2**: "CRUD Operations Skill example (CreateProfileCommand) compiles and handler resolves from DI"
      - Arrange: Load CRUD example files
      - Act: Build DI container, resolve CreateProfileCommandHandler
      - Assert: Resolves successfully, can be invoked
    - **Test 3**: "API Endpoints Skill example (ProfileV1Endpoints) endpoints register and respond to HTTP requests"
      - Arrange: Configure test HTTP client with example endpoints
      - Act: Send test HTTP requests
      - Assert: Endpoints respond with expected status codes and DTO shapes
    - **Test 4**: "Entity mutation methods from example follow class-first design (no public setters)"
      - Arrange: Load CustomerProfile entity from example
      - Act: Reflect on entity properties
      - Assert: Domain properties have private setters or no setters at all
    - **Test 5**: "Domain events raised during entity mutations are capturable"
      - Arrange: Create entity from example
      - Act: Call mutation method (Update()), access DomainEvents
      - Assert: Event is available and can be published
  - **Code Quality**:
    - [✓] xUnit patterns
    - [✓] Mix of unit and integration test styles
    - [✓] Tests exercise real examples (not mocks of examples)
  - **Acceptance Criteria**:
    - [✓] All 5+ test cases pass
    - [✓] Tests verify that skill-guided artifacts are production-ready
    - [✓] Coverage: all three skills represented
  - **Estimated Effort**: 90 min
  - **Dependencies**: T007, T012, T017

#### End-to-End Skill Usage Test

- [ ] **T026** End-to-end test: developer uses all 3 skills to implement a new feature
  - **File Path**: `src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/EndToEndSkillUsageTests.cs`
  - **Description**: Integration test demonstrating a complete feature workflow using all three skills in sequence. Simulate a developer following skill guidance step-by-step and verify the result is production-ready.
  - **Scenario**: "Add a new Order entity with full CRUD operations and REST API"
  - **Workflow Simulation**:
    - **Step 1 (Skill 1)**: Create Order entity + OrderMapper
      - Arrange: No existing Order entity
      - Act: Follow Domain Modeling Skill guidance to create entity and mapper
      - Assert: Mapper auto-discovered, entity compiles, migration script generated
    - **Step 2 (Skill 2)**: Create CRUD commands (Create, Update) + handlers
      - Arrange: Existing Order entity (from Step 1)
      - Act: Follow CRUD Operations Skill to create CreateOrderCommand, UpdateOrderCommand, handlers, specs, events
      - Assert: Commands execute, events publish, repository persists
    - **Step 3 (Skill 3)**: Create REST endpoints
      - Arrange: Existing commands + handlers (from Step 2)
      - Act: Follow API Endpoints Skill to create OrderV1Endpoints, DTOs, integrate
      - Assert: Endpoints respond correctly to HTTP requests (POST create, GET list, PUT update)
    - **Verification**:
      - All artifact files compile without warnings
      - Feature is independently testable and functional
      - Documentation (README in example) explains the complete workflow
  - **Test Code Structure**:
    - No mocking; use real test database and DI container
    - Full request-response cycle (HTTP client if applicable)
    - Verify that a fresh developer could replicate steps from skill guides
  - **Acceptance Criteria**:
    - [✓] Test passes end-to-end
    - [✓] New Order entity is fully functional (can create, read, update via API)
    - [✓] No warnings during build
    - [✓] Feature is independently deployable (all layers complete)
    - [✓] Test demonstrates practical value of skills (reduced rework, consistent output)
  - **Estimated Effort**: 120 min
  - **Dependencies**: T009, T014, T019

---

### PHASE 2G: Documentation & Training (Final Phase)

**Goal**: Enable team adoption through clear onboarding, migration guides, and reference documentation.  
**Effort**: 3–4 hours  
**MustCompete**: After all three skills are finalized (T022); can overlap with testing/validation.  
**Completion Criteria**: Guides published and tested with real developers (if possible).

---

#### Onboarding & Training Materials

- [ ] **T027** Create developer onboarding guide (30-min walkthrough)
  - **File Path**: `.github/copilot/skills/ONBOARDING.md`
  - **Description**: Guided walkthrough for a new team member to discover, understand, and use the skills pack. Include: navigation tips, success stories, common scenarios, and troubleshooting.
  - **Content Structure**:
    - **Welcome**: Brief overview of what skills are and why they matter
    - **Learning Path** (3 scenarios):
      - Scenario 1: "I'm starting a brand new feature from scratch" → Follow all three skills in sequence
      - Scenario 2: "I need to add endpoints to an existing entity" → Skip to Skill 3
      - Scenario 3: "I want to understand domain modeling best practices" → Start with Skill 1, focus on encapsulation
    - **30-Minute Walkthrough**:
      - Minute 0–5: Read CATALOG.md and pick a skill
      - Minute 5–10: Skim skill.md to understand high-level steps
      - Minute 10–28: Follow step-by-step workflow, refer to templates and examples
      - Minute 28–30: Check work against validation checklist
    - **FAQ**: Common questions ("Why are repositories sealed?", "When do I publish domain events?")
    - **Getting Help**: Link to team channel or reviewer contact
    - **Feedback**: Encourage developers to suggest skill improvements
  - **Tone**: Encouraging, non-condescending, assumes mid-level C# knowledge
  - **Acceptance Criteria**:
    - [✓] Walkthrough can be completed in ~30 min by a developer unfamiliar with project patterns
    - [✓] All navigation links work (no broken links)
    - [✓] Scenarios cover 80% of common feature work
    - [✓] FAQ answers are concise and link to deeper documentation if needed
  - **Estimated Effort**: 60 min
  - **Dependencies**: T020, T021, T005, T010, T015

- [ ] **T028** Create skill migration guide for existing code
  - **File Path**: `.github/copilot/skills/MIGRATION_GUIDE.md`
  - **Description**: Guidance for migrating existing, partially-guided code to fully skill-driven patterns. Help developers refactor older code to match profile mapper + CRUD + endpoint skill standards.
  - **Content Structure**:
    - **Migration Overview**: Why migrate (consistency, maintainability, team standardization)
    - **Per-Skill Migration Checklist**:
      - **Domain Modeling Migration**:
        - If: You have mappers in non-standard locations or inheriting from custom base
        - Then: Move to Minimal.Infra/Features/<Feature>/Mappers/, inherit from auto-config base
        - Checklist: 5 steps with examples
      - **CRUD Operations Migration**:
        - If: Commands in non-standard places, or events not being published
        - Then: Refactor to AppServices layer, add EventPublisher, encapsulate mutations
        - Checklist: 5 steps
      - **API Endpoints Migration**:
        - If: Endpoints not using IEndpointConfig or fluent mappers
        - Then: Refactor to fluent pattern, add OpenAPI docs
        - Checklist: 5 steps
    - **Common Migration Patterns**: Examples of before/after code
    - **Tooling**: Scripts or tools to help identify non-compliant code (if any)
    - **Review Checklist**: What reviewers should check during migration
  - **Acceptance Criteria**:
    - [✓] Migration guide covers all three skill areas
    - [✓] Step-by-step checklists are detailed enough to follow
    - [✓] Before/after code examples provided for each skill
    - [✓] No hidden assumptions (guide is self-contained)
  - **Estimated Effort**: 75 min
  - **Dependencies**: T005, T010, T015

- [ ] **T029** Create troubleshooting FAQ for skill usage
  - **File Path**: `.github/copilot/skills/FAQ.md`
  - **Description**: Q&A addressing common errors, confusion, and edge cases encountered when using skills. Include: error messages, causes, and solutions.
  - **FAQ Categories**:
    - **Discovery & Navigation**:
      - Q: "Where do I find skills?"
      - A: "Start at `.github/copilot/skills/README.md`"
    - **Domain Modeling**:
      - Q: "My mapper class isn't being auto-discovered. What's wrong?"
      - A: "Mapper must be sealed and in `Minimal.Infra/Features/<Feature>/Mappers/`. Check namespace and sealed keyword."
      - Q: "What's the difference between ConfigureProperty and HasOne?"
      - A: "ConfigureProperty configures scalar properties (strings, ints); HasOne configures relationships with other entities."
    - **CRUD Operations**:
      - Q: "Should business logic go in the entity or the command handler?"
      - A: "Encapsulated in the entity (e.g., Update() method); commands orchestrate and validate at layer boundary."
      - Q: "When do I raise domain events?"
      - A: "Raise in entity mutation methods (Create(), Update(), Delete()); publish in handlers via EventPublisher."
      - Q: "Do I need to test event publishing?"
      - A: "Yes; verify that DomainEvents are populated after mutations and are published via EventPublisher in handlers."
    - **API Endpoints**:
      - Q: "What's the [GenerateDto] attribute? Do I need to write DTO classes?"
      - A: "[GenerateDto] tells Mapster to auto-generate DTO classes from entity types. Write the attribute and DTOs are auto-created."
      - Q: "My endpoint returns 500 instead of 404 for not-found. How do I fix?"
      - A: "Ensure handler checks repository result and returns proper exception or result type (not throwing generic exception)."
      - Q: "How do I add OpenAPI documentation to endpoints?"
      - A: "Use ProduceResponseType, WithOpenApi(), and .WithSummary() fluent methods on endpoint mapping."
    - **Testing**:
      - Q: "What test coverage is expected for skills?"
      - A: "Domain entity + commands: ≥80%. Endpoints: ≥70%. Use xUnit + Shouldly patterns."
    - **General**:
      - Q: "Can I skip a skill if I only need part of it?"
      - A: "Partially, but skills are designed as a sequence. Skipping Skill 2 means no domain events or encapsulated mutations."
  - **Format**: Markdown with clear Q/A sections, links to relevant skill documentation
  - **Acceptance Criteria**:
    - [✓] FAQ has 15+ common questions
    - [✓] Each answer is concise and actionable
    - [✓] Answers link to relevant skill sections or examples
    - [✓] Tone is helpful (not dismissive)
  - **Estimated Effort**: 60 min
  - **Dependencies**: T005, T010, T015, T020

- [ ] **T030** Create maintenance runbook for skill lifecycle
  - **File Path**: `.github/copilot/skills/MAINTENANCE.md`
  - **Description**: Operations manual for skill maintainers. Covers: skill release process, deprecation rules, update procedures, and how to onboard new maintainers.
  - **Content Sections**:
    - **Skill Lifecycle Stages**:
      - Draft (internal development, not yet released)
      - Proposed (send announcement to team, request feedback)
      - Stable (published; available for all developers)
      - Deprecated (announce 2-release notice, plan removal)
      - Removed (archived in version control)
    - **Adding a New Skill** (5 steps with checklist):
      - Step 1: Create folder under .github/copilot/skills/ (follow CONVENTIONS.md)
      - Step 2: Draft skill.md, metadata.json, checklist.md
      - Step 3: Create templates and at least one working example
      - Step 4: Write tests in Minimal.App.Tests/Skills/
      - Step 5: Submit PR; ensure all validation CI checks pass; request review
    - **Updating an Existing Skill**:
      - Minor updates (docs, examples): direct update + test
      - Major changes (new templates, success criteria): follow issue tracking, announce update
      - Breaking changes (e.g., changing mapper base class): deprecate old skill, create new version
    - **Deprecating a Skill** (timeline & process):
      - Month 1: Announce deprecation in team standup and skill README.md
      - Month 2: Final release with deprecation notice
      - Month 3: Mark as deprecated in metadata.json; keep available
      - Month 4: Remove from active catalog; archive in repo
    - **Maintenance Schedule**:
      - Monthly: Review skill usage metrics, gather feedback
      - Per-release: Verify skill examples still compile, templates still follow latest patterns
      - Ad-hoc: Fix bugs or clarify confusing sections upon feedback
    - **PR Review Checklist for Skills** (for code reviewers):
      - [✓] skill.md step-by-step workflow is clear and complete
      - [✓] Templates are copy-paste-ready with TODO markers
      - [✓] Example represents real production code (sealed, correct namespaces, no warnings)
      - [✓] Tests cover happy path and errors
      - [✓] metadata.json validates against skill-schema.json
      - [✓] Checklist covers all success criteria
      - [✓] CATALOG.md and README.md updated (if new skill)
    - **Onboarding a New Maintainer**:
      - Read this file, CONVENTIONS.md, and existing skill.md examples
      - Add one new skill or feature to an existing skill (supervised by existing maintainer)
      - Review 2–3 skill PRs (with feedback)
      - After 3 months: confident independent maintainer
  - **Acceptance Criteria**:
    - [✓] Runbook covers full lifecycle (draft → stable → deprecated → removed)
    - [✓] Each process has clear steps and checklists
    - [✓] Timelines are realistic and documented
    - [✓] New maintainers can follow without needing additional context
  - **Estimated Effort**: 60 min
  - **Dependencies**: T002, T020

---

## Summary & Metrics

### Total Effort Estimate

| Phase                               | Tasks     | Effort (hours) | Parallelizable                                |
| ----------------------------------- | --------- | -------------- | --------------------------------------------- |
| **2A: Foundational Infrastructure** | T001–T004 | 2.5–3          | None (sequential)                             |
| **2B: Skill 1 (Domain Modeling)**   | T005–T009 | 4.5–5          | T005–T008 in parallel                         |
| **2C: Skill 2 (CRUD Operations)**   | T010–T014 | 6–7            | T010–T013 in parallel                         |
| **2D: Skill 3 (API Endpoints)**     | T015–T019 | 4.5–5          | T015–T018 in parallel                         |
| **2E: Catalog & Discovery**         | T020–T023 | 3–4            | T020–T021 in parallel; T023 depends on T022   |
| **2F: Testing & Validation**        | T024–T026 | 4–5            | T024–T025 in parallel; T026 depends on skills |
| **2G: Documentation & Training**    | T027–T030 | 3–4            | T027–T030 in parallel (after T022)            |
|                                     |           |                |
| **Total (Sequential)**              | 30 tasks  | 27–33 hours    | **~1 week (distributed)**                     |

### Parallelization Opportunities

**Critical Path** (longest dependency chain):
1. T001–T004 (foundational; 2.5 hrs)
2. T005–T008 (Skill 1 guidance; 3 hrs in parallel)
3. T010–T013 (Skill 2 guidance; 4 hrs in parallel; starts after T004)
4. T015–T018 (Skill 3 guidance; 3.5 hrs in parallel; starts after T004)
5. T022 (metadata generation; 0.75 hrs; depends on T005, T010, T015)
6. T020–T021 (catalog; 1.5 hrs in parallel; after T022)
7. T023 (CI validation; 1.5 hrs; depends on T022)

**Parallel tracks**:
- Skills 1, 2, 3 can be developed in parallel after T004 (templates + schema ready)
- Skills 2, 3 can start immediately after T001–T004 (no dependency on Skill 1 completion)
- Testing (T024–T025) can start after T004 (templates available) and run in parallel with skill development
- Core testing (T024–T025) done while skills develop
- End-to-end test (T026) requires all skills (T009, T014, T019)
- Documentation (T027-T030) can all be parallel after T022

**Realistic Schedule** (with parallel effort):
- **Week 1, Days 1–2**: T001–T004 (1 dev, foundation complete)
- **Week 1, Days 2–4**: T005–T008, T010–T013, T015–T018 (3 devs in parallel, skills documentation done)
- **Week 1, Days 3–4**: T024–T025 (parallel test suite, independent of skill completion)
- **Week 2, Day 1**: T009, T014, T019 (unit tests for each skill; can overlap with catalog work)
- **Week 2, Day 2–3**: T022–T023 (generate metadata, set up CI validation)
- **Week 2, Day 3**: T020–T021 (catalog + discovery)
- **Week 2, Day 4**: T026 (end-to-end test)
- **Week 2, Day 5**: T027–T030 (documentation + training, can overlap with above)

**Actual Days**: 5–6 days with 2–3 developers working in parallel.

---

## Acceptance & Success Criteria

### Phase 2 Definition of Done

**All Tasks Completed**:
- [✓] All 30 tasks in Phase 2 are marked complete (checkbox checked)
- [✓] All code compiles without warnings
- [✓] All tests pass (unit + integration)
- [✓] All documentation is published and discoverable

**Quality Gates**:
- [✓] Test coverage ≥80% for domain/command logic; ≥70% for endpoints
- [✓] All skill metadata.json validates against schema
- [✓] Folder structure complies with CONVENTIONS.md
- [✓] CI validation hooks (T023) are active and passing
- [✓] All file paths in tasks are accurate and files exist

**Developer Readiness Metrics** (testable):
- [✓] Can a new developer locate an appropriate skill in <2 min? (SC-001)
- [✓] Do skill-guided artifacts pass first-review quality checks? (SC-002, ≥85% first-pass rate)
- [✓] Can maintainers add new skill in <30 min following CONVENTIONS.md? (SC-004)
- [✓] Do 100% of published skills pass checklist before adoption? (SC-005)

**Deliverables Checklist**:
- [✓] `.github/copilot/skills/` folder structure complete with all subfolders
- [✓] CONVENTIONS.md, CATALOG.md, README.md, ONBOARDING.md, MIGRATION_GUIDE.md, FAQ.md, MAINTENANCE.md
- [✓] Three complete skills (domain-modeling, crud-operations, api-endpoints) with skill.md, metadata.json, templates/, examples/, checklist.md
- [✓] All tests passing: DomainModelingSkillTests, CrudOperationsSkillTests, ApiEndpointsSkillTests, SkillMetadataValidationTests, SkillGuidanceTests, EndToEndSkillUsageTests
- [✓] CI validation in place (.github/workflows/skills-validation.yml)

---

## Phase 3 & Beyond (Post-MVP)

**Not in this Phase 2, but planned for future**:
- Advanced skills (testing strategies, caching patterns, versioning guidance)
- GitHub Copilot Chat integration (@copilot commands)
- Web UI for skill browsing and discovery
- Skill usage analytics (which skills are most used by team)
- Templated code generation from skill guidance
- Video walkthroughs (if team prefers visual learning over written guides)
- Skill versions (e.g., sql-migrations-v1, sql-migrations-v2 with breaking changes)

---

## Notes for Implementation Team

1. **Start with T001–T004 first**, unblocking parallel skill development.
2. **Assign 3 developers** to skills 1, 2, 3 respectively; they can work in parallel after foundation complete.
3. **Testing can start early** (T024–T025) using test-driven approach (write tests first, then create artifacts).
4. **Use AGENTS.md** as source of truth for all pattern references (ProfileMapper, ProfileV1Endpoint, etc.); ensure consistency.
5. **Validate CI hooks** early (T023); catch metadata schema errors before finalizing skills.
6. **Gather feedback** from pilot developers during documentation phase (T027); iterate based on real usage.
7. **Keep all paths relative** (no absolute file references) so git clones work on any machine.
8. **Commit frequently**: Each task should be a logical commit; history will help future maintainers understand decisions.

---

**Generated**: 2026-03-17  
**Status**: Ready for implementation  
**Next Step**: Begin Phase 2A tasks (T001–T004)
