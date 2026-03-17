                                    ``   ````   `# Quick Start: Using DKNet.Templates Copilot Skills

**Date**: 2026-03-17  
**Output of**: `/speckit.plan` command Phase 1 section  
**Target Audience**: Developers (both experienced and new to DKNet.Templates)  
**Status**: Complete  

---

## TL;DR — 30-Second Start

**You need to add a new entity to DKNet.Templates?** Follow this:

1. **Entity with database mapping?** → Use **Domain Modeling Skill** (20 min)
2. **Entity with business logic?** → Use **CRUD Operations Skill** (45 min)  
3. **Entity with REST endpoints?** → Use **API Endpoints Skill** (30 min)

**Where to find skills?** Open `.github/skills/CATALOG.md` and search for your task.

**How to get help?** Ask Copilot: `@skills domain-modeling`

---

## 1. Finding a Skill

### Option A: Search CATALOG.md (Recommended)

```bash
# Open the file in your editor
.github/skills/CATALOG.md
```

**Quick Reference Table** shows:
- **Skill Name** (link to detailed skill.md)
- **Category** (Persistence, Logic, API, etc.)
- **Difficulty** (Beginner, Intermediate, Advanced)
- **Time Estimate** (20-30 min, 45-60 min, etc.)
- **When to Use** (clear guidance on what problem it solves)

**Search tips**:
- "I need to add a new entity" → **EFCore Mapping Configuration**
- "I need to build commands and business logic" → **CRUD Operations Implementation**
- "I need to create REST endpoints" → **API REST Endpoints Configuration**

### Option B: Ask Copilot (Fast)

```
User: @skills
Copilot: Lists all available skills with one-liners

User: @skills domain-modeling
Copilot: Shows full metadata + link to skill.md

User: @skills beginner
Copilot: Shows beginner-difficulty skills
```

### Option C: Browse README.md

```bash
.github/skills/README.md
```

Shows:
- **Quick Reference** (which skill for which task)
- **Recommended Workflows** (full-feature order)
- **Common Paths** (quick vs. detailed paths)
- **FAQs** (troubleshooting)

---

## 2. Understanding a Skill

Each skill has these files:

```
.github/skills/<skill-name>/
├── skill.md                 ← START HERE: step-by-step guide
├── metadata.json            ← machine-readable (ignore this)
├── checklist.md             ← quality gates (validate your work)
├── templates/               ← code templates to copy & customize
│   ├── mapper-template.cs
│   ├── entity-template.cs
│   └── ...
└── examples/                ← fully-worked, buildable example
    └── customer-profile-example/
        ├── CustomerProfile.cs
        ├── CustomerProfileMapper.cs
        └── README.md
```

### Reading a Skill: skill.md

**Structure** (all skills follow this):
1. **Goal** — What does this skill teach?
2. **Prerequisites** — What must I know first?
3. **Step-by-Step Walkthrough** — How to do the thing
4. **Code Templates** — Copy/paste starting points
5. **Worked Example** — Full, buildable reference
6. **Validation Checklist** — How to verify you're done
7. **Non-Goals** — What this skill does NOT cover
8. **Common Gotchas** — Mistakes to avoid

**Example**: Opening `domain-modeling/skill.md`:
```
# EFCore Mapping Configuration Skill

You will learn how to:
- Create a domain entity class
- Configure its EF Core mapping  
- Write a migration
- Validate that schema matches model

Prerequisites:
- Read AGENTS.md section "Architecture at a glance"
- Understand C# classes and properties
```

---

## 3. Following a Skill: Step-by-Step

### Step 1: Gather Inputs

Before starting, have these items ready:
- **Entity name** (e.g., "OrderHeader")
- **Properties** (field names and C# types)
- **Business rules** (validation, constraints)
- **Related entities** (foreign keys, navigation)

Example for OrderHeader:
```
Name: OrderHeader
Properties:
  - Id: Guid
  - CustomerId: Guid (FK to Customer)
  - OrderDate: DateTime
  - TotalAmount: decimal
  - Status: string (enum: Pending, Confirmed, Shipped, Completed)
  
Business Rules:
  - TotalAmount must be > 0
  - OrderDate must be ≤ today
  - Status transitions: Pending → Confirmed → Shipped → Completed
```

### Step 2: Copy Templates

From `templates/` folder, copy the template matching your entity:
```bash
# Example: Using Domain Modeling Skill
cp .github/skills/domain-modeling/templates/mapper-template.cs \
   src/SlimBus.Infra/Features/Orders/Mappers/OrderHeaderMapper.cs

# Customize the template
# Change "ProfileMapper" → "OrderHeaderMapper"
# Change "CustomerProfile" → "OrderHeader"
# Add your properties to the configuration
```

### Step 3: Follow the Walkthrough

The skill.md has numbered steps. Work through them in order:

**Example: Domain Modeling Skill steps:**
```
1. Create your entity class in Domains/Features/<Feature>/Entities/
   - Copy template from domain-modeling/templates/entity-template.cs
   - Add your properties
   - Add any validation logic in methods

2. Create your mapper in Infra/Features/<Feature>/Mappers/
   - Copy template from domain-modeling/templates/mapper-template.cs
   - Configure each property (datatype, length, required/optional)
   - Add indexes for query performance

3. Create a migration
   - Run: ./add-migration.sh AddOrderHeader
   - Review the migration SQL
   - Verify it matches your entity design

4. Test the migration
   - Run migration against local database
   - Verify schema in SQL Server matches your entity
```

### Step 4: Refer to Worked Example

If a step is unclear, check the worked example:

```
.github/skills/domain-modeling/examples/customer-profile-example/
├── CustomerProfile.cs       ← how properties look
├── CustomerProfileMapper.cs ← how mapper looks
└── README.md                ← explanation
```

**Copy pattern, not code**: Don't just copy the example; the example shows you the *pattern*. "How" and "Why" are explained; customize to your entity.

### Step 5: Validate Your Work

Use the checklist in the skill folder:

```bash
# Open the validation checklist
.github/skills/domain-modeling/checklist.md
```

Go through each checkbox:
- [ ] Entity class follows PascalCase naming
- [ ] Mapper inherits from correct base
- [ ] Located in correct folder (auto-discovery)
- [ ] All properties configured
- [ ] Compiles without warnings
- [ ] Migration runs successfully

**If any checkbox fails**, re-read that section of skill.md or ask Copilot: `@skills help with domain modeling`

### Step 6: Run Tests

Before submitting your PR:

```bash
# Compile with warnings-as-errors enforcement
dotnet build src/DKNet.Templates.sln -c Release

# Run all tests
dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings

# Check coverage
# (coverage report generated in coverage/ folder)
```

---

## 4. Full Workflow: Adding a New Feature (Example)

**Task**: Add an "Order" entity with full Create/Read/Update/Delete operations and REST endpoints.

**Time Estimate**: ~120 minutes | **Skills Used**: All three

### Phase 1: Domain Modeling (20-30 min)

**Goal**: Create `Order` entity and its database mapping

```bash
# 1. Open the skill guide
.github/skills/domain-modeling/skill.md

# 2. Follow steps 1-4 (entity class, mapper, migration, test)

# 3. Deliverables:
#    - src/SlimBus.Domains/Features/Orders/Entities/Order.cs
#    - src/SlimBus.Infra/Features/Orders/Mappers/OrderMapper.cs
#    - Migration: src/SlimBus.ApiEndpoints/Migrations/202603171538_AddOrder.cs

# 4. Validate using domain-modeling/checklist.md
```

### Phase 2: CRUD Operations (45-60 min)

**Goal**: Create commands, handlers, and events for Create/Update operations

```bash
# 1. Open the skill guide
.github/skills/crud-operations/skill.md

# 2. Follow steps 1-7 (commands, handlers, repo, events, tests)

# 3. Deliverables:
#    - CreateOrderCommand.cs + CreateOrderCommandHandler.cs
#    - UpdateOrderCommand.cs + UpdateOrderCommandHandler.cs
#    - OrderRepository.cs + IOrderRepository interface
#    - OrderCreatedEvent.cs + OrderUpdatedEvent.cs
#    - OrderTests.cs (unit tests using xUnit + Shouldly)

# 4. Validate using crud-operations/checklist.md
#    - Tests must pass with >80% coverage
```

### Phase 3: REST Endpoints (30-40 min)

**Goal**: Create HTTP endpoints for Create/Read/Update/Delete

```bash
# 1. Open the skill guide
.github/skills/api-endpoints/skill.md

# 2. Follow steps 1-5 (endpoints, DTOs, OpenAPI docs, tests)

# 3. Deliverables:
#    - OrderV1Endpoints.cs (IEndpointConfig implementation)
#    - OrderRequestDto.cs + OrderResponseDto.cs
#    - OrderEndpointsTests.cs (integration tests)

# 4. Validate using api-endpoints/checklist.md
#    - Integration tests must cover happy path + error cases
```

### Phase 4: Submit & Merge

```bash
# 1. Run full test suite
dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings

# 2. Build check
dotnet build src/DKNet.Templates.sln -c Release

# 3. Create PR with title:
#    "feat: add Order entity with CRUD operations and endpoints"

# 4. In PR description, reference which skills were used:
#    - Domain Modeling Skill ✓ (checklist passed)
#    - CRUD Operations Skill ✓ (tests >80% coverage)
#    - API Endpoints Skill ✓ (integration tests passed)

# 5. Code reviewer verifies against AGENTS.md patterns
# 6. Merge!
```

---

## 5. Common Paths (Pick Your Journey)

| Goal                                | Skills                             | Time        | Notes                                                  |
| ----------------------------------- | ---------------------------------- | ----------- | ------------------------------------------------------ |
| **Add simple read-only entity**     | Domain Modeling → API Endpoints    | 50 min      | No mutations, no CRUD skill needed                     |
| **Add full CRUD entity**            | Domain Modeling → CRUD → Endpoints | 120 min     | Most common path                                       |
| **Update business logic only**      | CRUD Operations                    | 30-45 min   | Entity + endpoints already exist; just modify commands |
| **Add endpoint to existing entity** | API Endpoints                      | 20-30 min   | Mapper and CRUD already done                           |
| **Create new aggregate root**       | Domain Modeling → CRUD → Endpoints | 120-150 min | Includes relationships, possibly multiple entities     |

---

## 6. Troubleshooting

### "I can't find the skill I need"

**Solution**:
1. Check CATALOG.md again (use Ctrl+F to search)
2. Ask Copilot: `@skills <keyword>` (e.g., `@skills validation`)
3. Check ".github/skills/README.md" Recommended Workflows section

### "The worked example doesn't match my entity"

**This is expected!** The example shows the *pattern*, not the exact code to copy.

**What to do**:
1. Read the example README.md explanation (it explains the pattern)
2. Look at the folder structure (that matters)
3. Look at the class relationships (that matters)
4. Customize the property names, types, and logic (that's OK!)

### "I don't understand a step in the skill"

**Solution**:
1. Re-read the "Why?" section (explains the reasoning)
2. Check the worked example (shows it in practice)
3. Look at existing code in src/ (follow established patterns)
4. Ask Copilot: `@skills help` (extended guidance)

### "My code doesn't pass warnings-as-errors check"

**Common issues**:
- Unused imports → Remove them
- Unused variables → Delete or use them
- Missing XML comments on public members → Add them
- Potential null reference → Add null checks

**Solution**:
```bash
# Build to see specific warnings
dotnet build src/DKNet.Templates.sln -c Release

# Fix each warning listed
# Re-run build to verify
```

### "My integration tests are failing"

**Common issues**:
- Test database not properly seeded
- Routes don't match HTTP method + path
- Response DTO doesn't match entity

**Solution**:
1. Check similar tests in SlimBus.App.Tests/ (use as reference)
2. Use debugger to inspect test failure details
3. Refer to api-endpoints/checklist.md "Integration Tests" section
4. Ask Copilot: `@skills help with endpoint testing`

### "Can I combine multiple skills in one PR?"

**Yes!** Most features use all three skills in sequence. That's the expected workflow.

One PR = one feature = Domain Modeling + CRUD + Endpoints (all three skills applied together)

---

## 7. Skill Maintenance (For Maintainers)

### Updating an Existing Skill

If the skill's example becomes outdated (e.g., EF Core API changes):

```bash
# 1. Update the example code
.github/skills/<skill-name>/examples/<example>/

# 2. Update the skill.md walkthrough if the steps changed
.github/skills/<skill-name>/skill.md

# 3. Run the example tests to verify
dotnet test .github/skills/<skill-name>/examples/*Tests.cs

# 4. Update metadata.json if duration/difficulty changed
.github/skills/<skill-name>/metadata.json

# 5. Verify it passes CI: all tests + no validation errors
```

### Adding a New Skill

To add the 4th skill (e.g., "Testing Strategies"):

```bash
# 1. Create folder structure
mkdir -p .github/skills/testing-strategies
mkdir -p .github/skills/testing-strategies/{templates,examples}

# 2. Copy template files from existing skill
# 3. Follow CONVENTIONS.md for structure/naming
# 4. Write skill.md with step-by-step guidance
# 5. Create worked example + tests
# 6. Write metadata.json + checklist.md
# 7. Verify CI passes (metadata validation + example tests)
# 8. CATALOG.md auto-regenerates (CI script handles it)
```

See `.github/skills/CONVENTIONS.md` for detailed requirements.

---

## 8. Getting Help

### Copilot Commands

```
@skills                           → List all skills
@skills domain                    → Filter by keyword
@skills beginner                  → Filter by difficulty
@skills <skill-name>              → Show specific skill details
@skills help                      → General skill usage help
@skills help with <topic>         → Topic-specific help
```

### Human Help

- **Slack**: #dknet-questions (ask about skills + architecture)
- **Code Review**: Mention @AGENT-MAINTAINER if skill guidance unclear
- **Bug Report**: Create issue: `skills: <skill-name> - <problem>`

### Finding More Info

- **AGENTS.md** — Deep dive on patterns referenced by skills
- **constitution.md** — Understand why patterns exist
- **Existing code** — Look at ProfileV1Endpoint, CustomerProfile for canonical examples

---

## Summary

1. **Find skill** → CATALOG.md search or `@skills` command
2. **Read skill**.md** → Understand goal, prerequisites, steps
3. **Copy templates** → Start from skill.md templates/ folder
4. **Follow walkthrough** → Work through numbered steps
5. **Check worked example** → Use customer-profile-example/ as reference
6. **Validate** → Go through checklist.md before submitting PR
7. **Test** → `dotnet test` + `dotnet build` must pass
8. **Submit** → PR mentions which skills were used; includes validation evidence

**Remember**: Skills are teaching tools, not code generators. You're learning the pattern AND building your feature at the same time. 🚀

