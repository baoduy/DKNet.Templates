# GitHub Copilot Skills for DKNet.Templates

**Reusable AI guidance for building vertical slice features in .NET 10 with ASP.NET Minimal APIs, EF Core, and domain-driven design.**

---

## 🚀 Quick Start (30 seconds)

You want to build a feature. Pick your starting point:

**If you're starting from scratch:**
```
1. Read: Domain Modeling Skill (20 min)     → Create your entity
2. Read: CRUD Operations Skill (45 min)     → Add business logic
3. Read: API Endpoints Skill (30 min)       → Expose via REST
```

**If you already have an entity:**
```
1. Start: CRUD Operations Skill             → Add commands/validators
2. Then: API Endpoints Skill                → Build REST endpoints
```

**If you need specific help:**
- Adding validators? → [CRUD Operations - Step 2](./crud-operations/skill.md#step-2-create-fluentvalidation-validators-for-each-request-dto)
- Creating endpoints? → [API Endpoints - Step 4](./api-endpoints/skill.md#step-4-create-endpoint-configuration)
- Entity mapping issues? → [Domain Modeling - Common Errors](./domain-modeling/skill.md#common-errors--how-to-fix-them)
- Creating or standardizing BDD scenarios? → [BDD Scenarios Skill](./dknet-bdd-tests/skill.md)

---

## 📚 All Skills

| Skill               | Duration  | Category       | Learn More                                    |
| ------------------- | --------- | -------------- | --------------------------------------------- |
| **Domain Modeling** | 20–30 min | Persistence    | [Read skill.md →](./domain-modeling/skill.md) |
| **CRUD Operations** | 45–60 min | Business Logic | [Read skill.md →](./crud-operations/skill.md) |
| **API Endpoints**   | 30–40 min | REST API       | [Read skill.md →](./api-endpoints/skill.md)   |
| **BDD Scenarios**   | 25–45 min | Testing        | [Read skill.md →](./dknet-bdd-tests/skill.md)   |

---

## 📖 How to Use a Skill

1. **Read the skill.md file** (5-10 min)
   - Understand the workflow in steps
   - See code examples for your specific use case
   - Check prerequisites

2. **Review the templates/** folder
   - Copy-customize the boilerplate code
   - Adjust names and properties for your entity

3. **Use the examples/** folder
   - See working production code (CustomerProfile)
   - Understand design decisions and patterns
   - Copy implementation patterns

4. **Run the checklist.md validation** (5 min)
   - Verify all requirements met
   - Fix any issues before testing
   - Ensures code quality gates

5. **Test end-to-end**
   - Run unit tests
   - Test endpoints in Swagger UI
   - Verify in Postman/VS Code

---

## 🎯 Success Examples

### Example 1: Build a Customer Profile Feature

```
Task: Add CRUD for customer profiles with validation and REST API

Step 1: Domain Modeling Skill (20 min)
├─ Create CustomerProfile entity
├─ Create CustomerProfileMapper
└─ Run migration

Step 2: CRUD Operations Skill (45 min)
├─ Create CreateCustomerProfileRequest DTO + validator
├─ Create UpdateCustomerProfileRequest DTO + validator
├─ Create ICustomerProfileRepository interface
├─ Implement repository in Infra layer
└─ Create CustomerProfileCreatedEvent

Step 3: API Endpoints Skill (30 min)
├─ Create CustomerProfileDto with [GenerateDto]
├─ Create ProfileV1Endpoints configuration
├─ Map all 4 CRUD endpoints (GET, POST, PUT, DELETE)
└─ Verify OpenAPI documentation

Result: ✅ Feature complete (1.5 hours)
        ✅ All validation passes
        ✅ API documented in Swagger
        ✅ Tests passing
```

---

## 🔍 Common Questions

<details>
<summary><strong>Q: When do I use [GenerateDto]?</strong></summary>

A: Use `[GenerateDto]` for **response DTOs** that mirror your entity exactly. It auto-generates property mappings. Use manual record types for **request DTOs** with validators.

[Learn more →](./api-endpoints/skill.md#step-1-create-response-dto-with-generatedto-attribute)

</details>

<details>
<summary><strong>Q: Do I need a validator for every request?</strong></summary>

A: Yes. Every request DTO should have a corresponding `AbstractValidator<T>` class. This enforces business rules at the API boundary.

[Learn more →](./crud-operations/skill.md#step-2-create-fluentvalidation-validators-for-each-request-dto)

</details>

<details>
<summary><strong>Q: My validator isn't running. Why?</strong></summary>

A: Check that `AppSetup.cs` calls:
```csharp
services.AddValidatorsFromAssembly(typeof(AppSetup).Assembly);
```

[Learn more →](./crud-operations/skill.md#step-7-register-validators-in-di-container)

</details>

<details>
<summary><strong>Q: Where should my repository go?</strong></summary>

A: 
- **Interface**: `Minimal.AppServices/Features/<Feature>/Repositories/IXxxRepository.cs`
- **Implementation**: `Minimal.Infra/Features/<Feature>/Repos/XxxRepository.cs` (sealed)

[Learn more →](./crud-operations/skill.md#step-3-create-repository-interface-in-appservices)

</details>

<details>
<summary><strong>Q: When do I publish domain events?</strong></summary>

A: After every state change in your entity. Publish in your service/handler after `SaveChangesAsync()`.

[Learn more →](./crud-operations/skill.md#step-5-create-domain-events)

</details>

---

## 📋 Validation Checklists

Each skill includes a checklist to ensure quality:

- **Domain Modeling**: 25 items (entity, mapper, migration, code quality)
- **CRUD Operations**: 18 items (DTOs, validators, repository, events)
- **API Endpoints**: 18 items (DTOs, validators, endpoints, OpenAPI)

Run the checklist before marking your feature complete:

```bash
# Open the checklist
code ./domain-modeling/checklist.md
# Go through each item
# Mark as complete when verified
```

---

## 🏗️ Architecture Overview

Skills are designed around **DKNet.Templates vertical slice architecture**:

```
API Layer (Minimal.Api)
  └─ ProfileV1Endpoints.cs           (Skill 3: API Endpoints)
     ├─ Request DTOs + Validators    (Skill 3: API Endpoints)
     └─ Response DTOs [GenerateDto]  (Skill 3: API Endpoints)

AppServices Layer (Minimal.AppServices)
  ├─ Request DTOs + Validators       (Skill 2: CRUD Operations)
  ├─ IRepository interface           (Skill 2: CRUD Operations)
  └─ Domain Events                   (Skill 2: CRUD Operations)

Domain Layer (Minimal.Domains)
  └─ CustomerProfile entity          (Skill 1: Domain Modeling)

Infra Layer (Minimal.Infra)
  ├─ CustomerProfileMapper           (Skill 1: Domain Modeling)
  └─ CustomerProfileRepository impl  (Skill 2: CRUD Operations)
```

---

## 🔗 Related Documentation

- [CONVENTIONS.md](./CONVENTIONS.md) - Skill development standards
- [CATALOG.md](./CATALOG.md) - Searchable skill index
- [AGENTS.md](../../../AGENTS.md) - DKNet.Templates architecture
- [FluentValidation Docs](https://docs.fluentvalidation.net/) - Validator patterns
- [EF Core Docs](https://docs.microsoft.com/en-us/ef/core/) - Mapper patterns

---

## 📞 Support

**Having issues?**

1. Check the skill's **Common Errors section** for your error
2. Read the **Prerequisites** - you may be missing a prior skill
3. Verify **checklist items** - something might not be wired
4. Check [AGENTS.md](../../../AGENTS.md) for architecture patterns

**Found a bug in a skill?**

- Open an issue with: skill name, error, reproduction steps
- Include checklist items that failed

---

## 📜 Skill Overview

**View the complete catalog**: [CATALOG.md](./CATALOG.md)

**Last Updated**: 2026-03-17  
**Status**: Published ✅  
**Total Time to Learn**: ~2 hours for all 3 skills
