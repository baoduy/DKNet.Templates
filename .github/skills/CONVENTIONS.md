# GitHub Copilot Skills Pack - Development Conventions

**Version**: 1.0.0  
**Last Updated**: 2026-03-17

---

## Overview

This document defines the standards and conventions for creating, maintaining, and deprecating reusable AI skills in the DKNet.Templates repository. All skills must follow these conventions to ensure discoverability, consistency, and compatibility with GitHub Copilot.

---

## Folder Structure Conventions

All skills are located under `.github/skills/` with a strict naming and organization pattern.

### Naming Convention

- **Skill folders**: kebab-case (lowercase, hyphens as word separators)
  - Examples: `domain-modeling`, `crud-operations`, `api-endpoints`
  - ❌ Avoid: `DomainModeling`, `domain_modeling`, `DOMAIN_MODELING`

- **File names**: lowercase with hyphens for multi-word
  - Examples: `skill.md`, `metadata.json`, `checklist.md`, `entity-template.cs`
  - ❌ Avoid: `Skill.md`, `Metadata.json`, `EntityTemplate.cs`

### Folder Structure

Each skill must follow this directory layout:

```
.github/copilot/skills/
├── domain-modeling/                      ← Skill folder (kebab-case)
│   ├── skill.md                         ← Main skill guidance (REQUIRED)
│   ├── metadata.json                    ← Machine-readable discovery (REQUIRED)
│   ├── checklist.md                     ← Validation gates (REQUIRED)
│   ├── templates/                       ← Copy-paste file templates (OPTIONAL)
│   │   ├── entity-template.cs
│   │   ├── mapper-template.cs
│   │   └── migration-template.sql
│   ├── examples/                        ← Working code examples (RECOMMENDED)
│   │   └── customer-profile-example/
│   │       ├── CustomerProfile.cs
│   │       ├── CustomerProfileMapper.cs
│   │       └── README.md
│   └── tests/                          ← Test files (OPTIONAL)
│       └── DomainModelingSkillTests.cs
│
├── _templates/                          ← Base templates for skill authors
│   ├── skill-template.md                ← Copy this to create new skill.md
│   ├── metadata-template.json           ← Copy this to create new metadata.json
│   ├── checklist-template.md            ← Copy this to create new checklist.md
│   └── skill-schema.json                ← Validation schema for metadata.json
│
├── CONVENTIONS.md                       ← This file
├── CATALOG.md                           ← Auto-generated index of all skills
└── README.md                            ← Quick-start guide + skill overview
```

---

## File Naming Reference

| File            | Required    | Purpose                                                                 | Format                                           |
| --------------- | ----------- | ----------------------------------------------------------------------- | ------------------------------------------------ |
| `skill.md`      | ✅ Required  | Step-by-step procedural guidance for the skill                          | Markdown with structured sections                |
| `metadata.json` | ✅ Required  | Machine-readable skill metadata for discovery and validation            | JSON conforming to `skill-schema.json`           |
| `checklist.md`  | ✅ Required  | Quality gates and acceptance criteria for completing the skill          | Markdown with checkbox items                     |
| `templates/`    | Optional    | Copy-paste code templates developers customize for their use case       | Language-specific files (.cs, .sql, .yaml, etc.) |
| `examples/`     | Recommended | Complete, working example of the skill applied to a real entity/feature | Full source code with README explanation         |
| `tests/`        | Optional    | Unit and integration tests validating skill template correctness        | Language-specific test files (.cs)               |

---

## Mandatory Metadata Fields (metadata.json)

Every skill's `metadata.json` MUST include these fields:

```json
{
  "id": "domain-modeling",
  "title": "Domain Modeling with EFCore Mapping Configuration",
  "category": "Persistence & Entities",
  "difficulty": "Intermediate",
  "estimatedDurationMinutes": { "min": 20, "max": 30 },
  "prerequisites": ["Familiarity with C# classes", "Understanding of EF Core basics"],
  "inputs": {
    "description": "Information needed before starting",
    "items": ["Entity name", "Properties list", "Relationships", "Validation rules"]
  },
  "outputs": {
    "description": "What you will have created",
    "items": ["Domain entity class", "EF Core mapper class", "Database migration"]
  },
  "successCriteria": [
    "Entity class follows class-first design with encapsulated state",
    "Mapper is auto-discoverable in Scrutor scanning",
    "All properties validations are configured in EF model",
    "Migration applies without errors"
  ],
  "nonGoals": [
    "Testing the entity (see CRUD Operations Skill for business logic tests)",
    "Building API endpoints (see API Endpoints Skill for that)"
  ],
  "relatedSkills": ["crud-operations", "api-endpoints"],
  "folderPath": ".github/skills/domain-modeling",
  "examplesPath": ".github/skills/domain-modeling/examples/customer-profile-example",
  "testPath": "src/SlimBus.ApiEndpoints/SlimBus.App.Tests/Skills/DomainModelingSkillTests.cs"
}
```

### Field Definitions

| Field                      | Type                | Required | Description                                                                               |
| -------------------------- | ------------------- | -------- | ----------------------------------------------------------------------------------------- |
| `id`                       | string (kebab-case) | ✅ Yes    | Unique identifier; matches folder name                                                    |
| `title`                    | string              | ✅ Yes    | Human-readable skill name (60 chars max)                                                  |
| `category`                 | enum                | ✅ Yes    | One of: `Persistence & Entities`, `Business Logic & Commands`, `REST API & Orchestration` |
| `difficulty`               | enum                | ✅ Yes    | One of: `Beginner`, `Intermediate`, `Advanced`                                            |
| `estimatedDurationMinutes` | object              | ✅ Yes    | `{ "min": number, "max": number }`                                                        |
| `prerequisites`            | array               | ✅ Yes    | List of prerequisites (at least 1)                                                        |
| `inputs`                   | object              | ✅ Yes    | `{ "description": string, "items": [strings] }`                                           |
| `outputs`                  | object              | ✅ Yes    | `{ "description": string, "items": [strings] }`                                           |
| `successCriteria`          | array               | ✅ Yes    | Checklist items (at least 3); must be testable                                            |
| `nonGoals`                 | array               | ✅ Yes    | Out-of-scope items; prevents scope creep                                                  |
| `relatedSkills`            | array               | ✅ Yes    | IDs of other skills; can be empty `[]`                                                    |
| `folderPath`               | string              | ✅ Yes    | Relative path to skill folder from repo root                                              |
| `examplesPath`             | string              | Optional | Relative path to examples folder if included                                              |
| `testPath`                 | string              | Optional | Relative path to test file if included                                                    |

---

## Skill Document Structure (skill.md)

Every `skill.md` must follow this structure (use `_templates/skill-template.md` as your starting point):

### Required Sections

1. **Overview** — When and why to use this skill
2. **Prerequisites** — What knowledge/tools are required
3. **Inputs Checklist** — Information you need to gather before starting
4. **Step-by-Step Workflow** — Numbered, independently followable steps
5. **Success Validation** — Link to `checklist.md` and key acceptance criteria
6. **Common Errors & Fixes** — Troubleshooting guide
7. **Examples** — Reference to `examples/` folder; shown in output
8. **Next Steps** — Related skills and natural workflow progression

### Writing Guidelines

- **Audience**: Mid-level developers familiar with C# and DKNet.Templates architecture
- **Clarity**: Each step must be independently actionable with 5-10 minute focus time
- **No tribe knowledge**: Always link to AGENTS.md or other documentation; don't assume readers have memorized project patterns
- **Code snippets**: Use triple-backticks with language highlighting; include comments explaining "why" not just "what"
- **Links**: Link to AGENTS.md, this file, relevant examples, and templates

### Example Structure

```markdown
# Skill: Domain Modeling with EFCore Mapping Configuration

## Overview

Use this skill when you need to add a new database entity to the application. This skill guides you through...

## Prerequisites

Before starting, you should:
- Read [AGENTS.md - Feature Vertical Slice Pattern](../../../AGENTS.md)
- Understand C# classes and properties
- Be familiar with dependency injection

## Inputs Checklist

Gather this information before starting:
- [ ] Entity name (PascalCase, e.g., "CustomerProfile")
- [ ] List of properties (name, type, required/optional)
- [ ] Relationships to other entities (if any)
- [ ] Validation rules (string lengths, numeric ranges)
- [ ] Query patterns (indexes needed for common filters)

## Step-by-Step Workflow

### Step 1: Create Entity Class in Domains Layer
...

### Step 2: Implement Mutation Methods
...

[etc.]

## Success Validation

Verify your work against the [Validation Checklist](./checklist.md):
- [ ] Entity class placed correctly in Domains layer
- [ ] Mapper class is sealed and auto-discoverable
- [ ] All properties configured with EF Core constraints
- [ ] Migration applies cleanly to database

## Common Errors & Fixes

### Error: "Mapper class not auto-discovered"
**Cause**: Mapper not in expected location or not sealed  
**Fix**: Move to `SlimBus.Infra/Features/<Feature>/Mappers/` and add `sealed` keyword

[etc.]

## Examples

See [customer-profile-example/](./examples/customer-profile-example/) for a complete, production-ready example:
- [CustomerProfile.cs](./examples/customer-profile-example/CustomerProfile.cs) — Entity class
- [CustomerProfileMapper.cs](./examples/customer-profile-example/CustomerProfileMapper.cs) — EF mapper
- [README.md](./examples/customer-profile-example/README.md) — Explanation of each piece

## Next Steps

Once your entity + mapper is complete:
1. **Test & Validate**: Run the [validation checklist](./checklist.md)
2. **Add Business Logic**: Follow the [CRUD Operations Skill](../crud-operations/skill.md)
3. **Expose via API**: Follow the [API Endpoints Skill](../api-endpoints/skill.md)
```

---

## Validation Checklist Structure (checklist.md)

Every `checklist.md` must:

- Use checkbox format: `- [ ] Item` (easy copy-paste into issue templates)
- Organize by category (Entity Class, Property Mapping, Migration & Schema, Code Quality)
- 10+ items minimum to ensure comprehensive quality gates
- Include remediation guidance for each failed item
- Reference templates and examples for guidance

---

## Skill Lifecycle and Status

### Status Values

Each skill has a lifecycle status tracked in `metadata.json` (optional field):

- **`draft`** — In development; not yet ready for team use
- **`published`** — Ready for use; appears in index
- **`deprecated`** — No longer recommended; will be removed in 2 releases
- **`removed`** — No longer supported; deleted from repository

### Publishing Process

1. **Draft**: Create folder, write skill.md, create templates and examples
2. **Validation**: Ensure all checklist items pass; run tests
3. **Review**: Obtain approval from lead architect or maintainer
4. **Publish**: Move to `published` status in metadata.json; add to CATALOG.md
5. **Announce**: Link to skill in team Slack/wiki/docs

### Deprecation Process

1. **Announce**: Communicate to team; link replacement skill if applicable
2. **Maintain**: Keep working for 2 release cycles (backwards compatible)
3. **Remove**: Delete folder after 2 releases; remove fromCAT ALOG.md

---

## Versioning Rules

Skills use **semantic versioning** within their `metadata.json`:

```json
"version": "1.2.3"
```

- **MAJOR** (1.0.0 → 2.0.0): Breaking change (e.g., new required input, changed output format)
- **MINOR** (1.0.0 → 1.1.0): New non-breaking addition (e.g., new template, new step)
- **PATCH** (1.0.0 → 1.0.1): Bug fix or clarification (e.g., typo, example correction)

---

## Quality Gates for Skill Approval

Before a skill is marked `published`, it must pass:

- ✅ All `metadata.json` fields present and conform to `skill-schema.json`
- ✅ `skill.md` includes all mandatory sections and is < 2000 words
- ✅ `checklist.md` has 10+ items with remediation guidance
- ✅ At least one complete working example in `examples/` with README
- ✅ All code examples compile without warnings
- ✅ Template files have TODO/CHANGEME markers and inline guidance
- ✅ Tests pass (if `testPath` provided)
- ✅ Links to AGENTS.md and related skills verified

---

## Maintenance and Updates

### Updating an Existing Skill

1. Edit the skill files (skill.md, templates, examples)
2. Bump version in `metadata.json` per semantic versioning rules
3. Update CATALOG.md if description changed
4. Commit with message: `docs(skills): update <skill-id> to v<new-version> — <reason>`

### Adding a New Skill

1. Create folder under `.github/skills/` with kebab-case name
2. Copy from `_templates/skill-template.md`, `_templates/metadata-template.json`, `_templates/checklist-template.md`
3. Follow all sections above (metadata fields, documentation structure)
4. Create at least one example in `examples/`
5. Run: `scripts/validate-skill.sh <skill-folder>` (automates schema validation)
6. Obtain review approval
7. Merge PR with commit message: `feat(skills): add <skill-id> v<version> — <description>`

### CI/CD Integration

A GitHub Actions workflow auto-validates all skills:

```bash
# Validates all metadata.json files against skill-schema.json
# Runs on every PR to .github/skills/
scripts/validate-skills.sh
```

---

## Copilot Integration Paths

### Path 1: Developer Discovery (current)
Developers browse `.github/skills/CATALOG.md` and follow guides directly.

### Path 2: GitHub Copilot Chat (future)
```
@copilot /skill domain-modeling

"I need to create a new entity called Order. Guide me through it."
```

### Path 3: CLI Tool (future)
```bash
copilot-skills list
copilot-skills show domain-modeling
copilot-skills search "add entity"
```

---

## Questions? Need Help?

- 📖 See [README.md](./README.md) for quick-start guide
- 🔍 Search [CATALOG.md](./CATALOG.md) for specific topics
- 🐛 Found a bug in a skill? Open an issue with: `[SKILL] <skill-id>: <issue>`
- 💡 Want to add a new skill? Read **Adding a New Skill** section above
- 👥 Questions about conventions? Comment in PR or ping maintainers

---

**Last Updated**: 2026-03-17 | **Maintained By**: DKNet.Templates Team
