# Copilot Skills Catalog

**Quick Index** | **View by Category** | **Difficulty Levels** | **Search by Tag**

---

## All Available Skills

### 1. Domain Modeling with EF Core

- **ID**: `domain-modeling`
- **Category**: Persistence & Entities
- **Difficulty**: Intermediate
- **Duration**: 20–30 minutes
- **Purpose**: Create domain entities and EF Core mapper configurations
- **Inputs**: Entity name, properties, relationships, validation rules
- **Outputs**: Entity class, mapper with configuration, migration
- **Prerequisites**: [Domain Modeling Skill](./domain-modeling/skill.md) completion
- **Success Criteria**: 
  - Entity class is sealed with init properties
  - Mapper auto-discovered by Scrutor
  - Migration applies cleanly
  - Zero compiler warnings
- **Related Skills**: crud-operations, api-endpoints
- **Folder**: [`.github/copilot/skills/domain-modeling/`](./domain-modeling/)
- **Example**: [CustomerProfile entity example](./domain-modeling/examples/customer-profile-example/)

**Start Here**: Read [Domain Modeling skill.md](./domain-modeling/skill.md)

---

### 2. CRUD Operations

- **ID**: `crud-operations`
- **Category**: Business Logic & Commands
- **Difficulty**: Intermediate
- **Duration**: 45–60 minutes
- **Purpose**: Implement Create, Read, Update, Delete operations with DTOs, validators, and domain events
- **Inputs**: Domain entity (from Skill 1), business rules, query patterns, events to publish
- **Outputs**: Request/response DTOs, validators, repository interface/implementation, domain events, services
- **Prerequisites**: [CRUD Operations Skill](./crud-operations/skill.md) - requires completed Skill 1
- **Success Criteria**:
  - Request DTOs have FluentValidation validators
  - Repository interface in AppServices, implementation in Infra
  - Domain events published after mutations
  - All validators auto-discovered
  - Zero compiler warnings
- **Related Skills**: domain-modeling, api-endpoints
- **Folder**: [`.github/skills/crud-operations/`](./crud-operations/)
- **Example**: [CustomerProfile CRUD example](./crud-operations/examples/customer-profile-crud/)

**Start Here**: Read [CRUD Operations skill.md](./crud-operations/skill.md)

---

### 3. API REST Endpoints

- **ID**: `api-endpoints`
- **Category**: REST API & Orchestration
- **Difficulty**: Intermediate
- **Duration**: 30–40 minutes
- **Purpose**: Expose CRUD operations via REST endpoints with OpenAPI documentation
- **Inputs**: Entity with CRUD operations ready (from Skills 1 & 2), API version, response models
- **Outputs**: Endpoint configuration, DTOs with [GenerateDto], validators, OpenAPI documentation
- **Prerequisites**: [API Endpoints Skill](./api-endpoints/skill.md) - requires completed Skills 1 & 2
- **Success Criteria**:
  - All CRUD endpoints functional (GET, POST, PUT, DELETE)
  - Response DTOs auto-generated with [GenerateDto] attribute
  - Request DTOs have validators
  - OpenAPI/Swagger documentation complete
  - Status codes correct (201, 204, 404, etc.)
- **Related Skills**: crud-operations, domain-modeling
- **Folder**: [`.github/skills/api-endpoints/`](./api-endpoints/)
- **Example**: [Profile V1 Endpoints example](./api-endpoints/examples/profile-endpoints-example/)

**Start Here**: Read [API Endpoints skill.md](./api-endpoints/skill.md)

---

### 4. Feature Documentation with Diagrams

- **ID**: `feature-documentation`
- **Category**: Documentation & Knowledge Management
- **Difficulty**: Beginner
- **Duration**: 30–60 minutes
- **Purpose**: Generate structured technical documentation and Mermaid architecture diagrams for a completed feature
- **Inputs**: A completed feature (entity, handlers, endpoints, events) and its feature name
- **Outputs**: 5 Markdown docs under `docs/features/<feature-name>/` (README, architecture, api-reference, data-model, events)
- **Prerequisites**: Feature implementation complete (Skills 1–3)
- **Success Criteria**:
  - All 5 docs present and filled with real feature details
  - All Mermaid diagrams render correctly in GitHub / VS Code
  - API reference has curl examples for every endpoint
  - Data model matches actual EF Core mapping
  - Events catalog lists all `IHandler<TEvent>` subscribers
- **Related Skills**: domain-modeling, crud-operations, api-endpoints
- **Folder**: [`.github/skills/feature-documentation/`](./feature-documentation/)
- **Example**: [Customer Profiles example docs](../../docs/features/customer-profiles/)

**Start Here**: Read [Feature Documentation skill.md](./feature-documentation/skill.md)

---

## Skills by Category

### Persistence & Entities
- [Domain Modeling](./domain-modeling/skill.md) - 20-30 min

### Business Logic & Commands
- [CRUD Operations](./crud-operations/skill.md) - 45-60 min

### REST API & Orchestration
- [API REST Endpoints](./api-endpoints/skill.md) - 30-40 min

### Documentation & Knowledge Management
- [Feature Documentation with Diagrams](./feature-documentation/skill.md) - 30-60 min

---

## Skills by Difficulty

### Beginner
- Feature Documentation with Diagrams (30-60 min)

### Intermediate
- Domain Modeling (20-30 min)
- CRUD Operations (45-60 min)
- API REST Endpoints (30-40 min)

---

## Recommended Learning Path

```
1. Domain Modeling Skill (20-30 min)
   └─ Learn to create entities with EF Core mappers
   
2. CRUD Operations Skill (45-60 min)
   └─ Add business logic, validators, repositories, events
   
3. API Endpoints Skill (30-40 min)
   └─ Expose functionality via REST API with documentation

4. Feature Documentation Skill (30-60 min)
   └─ Generate README, architecture diagrams, API reference, data model, and events catalog
```

**Total Time**: ~2.5–3 hours for a complete vertical slice with documentation

---

## Common Workflows

### "I need to build a new feature from scratch"

1. **Start with Domain Modeling**: 
   - Create your entity in `SlimBus.Domains` with your mapper in `SlimBus.Infra`
   - See [Domain Modeling skill.md](./domain-modeling/skill.md)

2. **Add Business Logic with CRUD**:
   - Create request DTOs with validators in `SlimBus.AppServices`
   - Create repository and domain events
   - See [CRUD Operations skill.md](./crud-operations/skill.md)

3. **Expose via API**:
   - Create endpoints in `SlimBus.Api` with OpenAPI docs
   - See [API Endpoints skill.md](./api-endpoints/skill.md)

### "I need to add validation to my DTOs"

→ See [CRUD Operations Skill - Step 2: Create FluentValidation Validators](./crud-operations/skill.md#step-2-create-fluentvalidation-validators-for-each-request-dto)

→ See [API Endpoints Skill - Step 2: Create Validators for All Request DTOs](./api-endpoints/skill.md#step-2-create-validators-for-all-request-dtos)

### "I need to add an API endpoint for an existing entity"

→ See [API Endpoints Skill - Step 4: Create Endpoint Configuration](./api-endpoints/skill.md#step-4-create-endpoint-configuration)

### "I need to document a completed feature"

→ See [Feature Documentation skill.md](./feature-documentation/skill.md)
→ Output goes to `docs/features/<feature-name>/` (README + architecture + api-reference + data-model + events)

### "I need architecture diagrams for my feature"

→ See [Feature Documentation Skill - Step 3: Write architecture.md](./feature-documentation/skill.md#step-3-write-architecturemd)
→ Uses Mermaid diagrams: vertical slice, sequence, class, state machine, event flow

---

## Validation & Quality Gates

Each skill includes:
- **Checklist** (`.md` file) with 15-25 validation items
- **Templates** (boilerplate code to customize)
- **Examples** (working production code)

**Before marking a skill complete**, verify all checklist items:
- [ ] Request/Response DTOs created
- [ ] Validators configured
- [ ] Repository pattern implemented
- [ ] Domain events published
- [ ] OpenAPI documentation present
- [ ] Zero compiler warnings
- [ ] Unit tests passing

---

## Metadata & Discovery

All skills use machine-readable **metadata.json** files for automated discovery:

```json
{
  "id": "domain-modeling",
  "title": "Domain Modeling with EF Core",
  "category": "Persistence & Entities",
  "difficulty": "Intermediate",
  "estimatedDurationMinutes": { "min": 20, "max": 30 },
  "successCriteria": [...]
}
```

Future integrations (CLI, Chat) will use this metadata for:
- Skill discovery: `@copilot /skill domain-modeling`
- Automated help: `@copilot help crud-operations`
- Progress tracking: `@copilot status 001-copilot-skills-pack`

---

## Contributing New Skills

To add a new skill:

1. **Read** [CONVENTIONS.md](./CONVENTIONS.md) - Development standards
2. **Copy** [_templates/](./\_templates/) folder structure
3. **Create** your skill folder with:
   - `skill.md` - Step-by-step workflow
   - `metadata.json` - Machine-readable discovery data
   - `checklist.md` - Validation gates
   - `templates/` - Boilerplate code
   - `examples/` - Working production code
4. **Validate** against JSON Schema (enforced in CI)
5. **Test** skill workflow end-to-end
6. **Update** this CATALOG.md with your skill entry

See [CONVENTIONS.md - Publishing Process](./CONVENTIONS.md#publishing-process) for details.

---

## Support & Troubleshooting

### "My validator isn't being discovered"

→ Ensure validator class name follows pattern: `<RequestDtoName>Validator`  
→ Verify class inherits from `AbstractValidator<T>`  
→ Check `AppSetup.cs` calls `AddValidatorsFromAssembly(typeof(AppSetup).Assembly)`

### "My entity mapper isn't auto-registering"

→ Ensure mapper class is `sealed` and in `SlimBus.Infra/Features/<Feature>/Mappers/`  
→ Check mapper implements `IEntityTypeConfiguration<T>`  
→ Verify `UseAutoConfigModel()` in DbContext configuration

### "My endpoint returns 400 Bad Request"

→ Check your request validators - validation may be failing  
→ Verify `AddValidatorsFromAssembly()` in AppSetup.cs  
→ Use Swagger UI to test with sample payload

---

**Last Updated**: 2026-03-17  
**Status**: Published  
**Maintainer**: DKNet.Templates Team
