# Implementation Plan Summary: Copilot Skills Pack (001)

**Feature**: Feature 001-copilot-skills-pack — Build reusable GitHub Copilot skills for developers  
**Branch**: `001-copilot-skills-pack`  
**Planning Date**: 2026-03-17  
**Status**: ✅ **Planning Complete** (Phase 0 + Phase 1)  
**Next Phase**: Phase 2 (Implementation Tasks) — Run `/speckit.tasks` command

---

## 📋 What Was Planned

This document serves as the comprehensive implementation plan for the Copilot Skills Pack feature. It includes:

1. **Detailed Technical Architecture** — How skills integrate with DKNet.Templates
2. **Folder Structure & File Organization** — Where skills live and how they're organized
3. **Three Core Skills Defined** — Complete specifications with use cases and success criteria
4. **Discovery & Maintenance Framework** — CATALOG.md, CONVENTIONS.md, validation patterns
5. **Design Artifacts** — Skill metadata schema, API contracts, validation checklists
6. **Developer Onboarding** — Quick-start guide and common workflows
7. **Research Findings** — All major design decisions documented with rationale

---

## 🎯 Key Design Decisions

### 1. Location: `.github/skills/`
- **Why**: GitHub's conventional location for integrations (aligns with `.github/workflows/`, `.github/ISSUE_TEMPLATE/`)
- **Benefit**: Discoverable, version-controlled, offline-accessible

### 2. Three Foundational Skills (MVP)
- **Domain Modeling** (Infra + Domains): Entity + EF Core mapping
- **CRUD Operations** (AppServices + Domains): Commands, events, repositories
- **API Endpoints** (Api): REST routes, DTOs, OpenAPI docs
- **Why Three?**: Aligns with DKNet.Templates layers; covers 80% of feature use cases

### 3. Skill Architecture: skill.md + templates/ + examples/ + metadata.json + checklist.md
- **skill.md**: Step-by-step procedural guidance
- **templates/**: Copy-customize code starting points
- **examples/**: Fully worked, buildable reference with tests
- **metadata.json**: Machine-readable, enables catalog generation & tool integration
- **checklist.md**: Human-readable quality gates for self-service validation

### 4. Validation: Hand-Crafted Templates + Example Tests + CI Gate
- **No code generation frameworks** (keeps code explicit and readable)
- **Example tests in CI** (proves examples don't stale)
- **Per-skill checklist.md** (self-service + repeatable review)
- **CONVENTIONS.md** (single source of truth for maintainability rules)

### 5. Discoverability: CATALOG.md + README.md + skills-config.json
- **CATALOG.md**: Human-searchable table (category, difficulty, use-when)
- **README.md**: Quick-start guide + recommended workflows
- **skills-config.json**: Auto-generated from metadata; enables `@skills` Copilot command

---

## 📁 Complete Project Structure

```
.github/skills/              ← Skills root
├── README.md                         ← Catalog guide + quick-start
├── CATALOG.md                        ← Searchable index of all skills
├── CONVENTIONS.md                    ← Maintenance rules (for skill authors)
├── skills-config.json                ← Auto-generated registry (do not edit)
└── <skill-name>/
    ├── skill.md                      ← Step-by-step guidance
    ├── metadata.json                 ← Discoverable metadata
    ├── checklist.md                  ← Validation gates
    ├── templates/                    ← Code templates to copy
    │   ├── entity-template.cs
    │   ├── mapper-template.cs
    │   └── ...
    └── examples/                     ← Fully-worked reference
        └── <entity>-example/
            ├── <Entity>.cs
            ├── <Entity>Mapper.cs
            ├── <Entity>Tests.cs
            └── README.md

Skill Folders (concrete paths):

.github/copilot/skills/domain-modeling/
  ├── skill.md
  ├── metadata.json
  ├── checklist.md
  ├── templates/
  │   ├── mapper-template.cs
  │   ├── entity-template.cs
  │   └── migration-template.sql
  └── examples/customer-profile-example/

.github/copilot/skills/crud-operations/
  ├── skill.md
  ├── metadata.json
  ├── checklist.md
  ├── templates/
  │   ├── entity-template.cs
  │   ├── command-template.cs
  │   ├── spec-template.cs
  │   ├── repository-template.cs
  │   └── event-template.cs
  └── examples/customer-profile-crud/

.github/copilot/skills/api-endpoints/
  ├── skill.md
  ├── metadata.json
  ├── checklist.md
  ├── templates/
  │   ├── endpoint-template.cs
  │   ├── mapping-helpers.cs
  │   └── openapi-template.yaml
  └── examples/customer-profile-endpoints/

Test Integration:

src/Minimal.ApiEndpoints/Minimal.App.Tests/Skills/
  ├── DomainModelingSkillTests.cs
  ├── CrudOperationsSkillTests.cs
  └── ApiEndpointsSkillTests.cs

Contracts (Design Artifacts):

specs/001-copilot-skills-pack/contracts/
  ├── skill-schema.json                ← Metadata validation schema
  ├── catalog-api.yaml                 ← Future portfolio/tool integration API
  └── validation-checklist-schema.json ← CI validation schema
```

---

## 📚 Skills Specifications

### Skill 1: Domain Modeling with EFCore Mapping Configuration
- **Folder**: `.github/skills/domain-modeling/`
- **Duration**: 20-30 minutes
- **What it teaches**: Creating domain entities and EF Core mappings using auto-configuration
- **Outputs**: Entity class + Mapper + Migration script
- **Success Criteria**: 10 checkpoints (entity design, mapping patterns, auto-discovery, tests)
- **Example**: CustomerProfile + CustomerProfileMapper (fully worked, tested)

### Skill 2: CRUD Operations Implementation  
- **Folder**: `.github/skills/crud-operations/`
- **Duration**: 45-60 minutes
- **What it teaches**: Building Create/Read/Update/Delete with commands, repositories, domain events
- **Outputs**: Commands + handlers, Repository, Domain events, Unit tests
- **Success Criteria**: 11 checkpoints (encapsulation, layer separation, event publishing, test coverage >80%)
- **Example**: CustomerProfile CRUD (all four operations fully worked, tested)

### Skill 3: API REST Endpoints Configuration
- **Folder**: `.github/skills/api-endpoints/`
- **Duration**: 30-40 minutes
- **What it teaches**: Wiring commands/queries to HTTP endpoints, OpenAPI documentation
- **Outputs**: Endpoints + DTOs + OpenAPI annotations + Integration tests
- **Success Criteria**: 9 checkpoints (fluent mappers, DTO patterns, OpenAPI docs, integration tests)
- **Example**: ProfileV1Endpoints (GET, POST, PUT, DELETE fully worked, tested)

---

## 🔗 Design Artifacts Generated

### Phase 1 Deliverables (Complete)

1. **plan.md** ✅ (this file)
   - Technical context filled
   - Constitution check passed
   - Project structure defined
   - Complexity tracking (none - no violations)
   - Phase 0-2 planning sections

2. **research.md** ✅  
   - Folder structure decision (`.github/skills/`)
   - Metadata format (JSON + checklist.md)
   - Skill scope (3 foundational skills)
   - Testing approach (worked examples + tests)
   - Template strategy (hand-crafted)
   - Maintenance model (CONVENTIONS.md + CI validation)
   - Discoverability framework (CATALOG.md + README.md)
   - All unknowns resolved; no blockers

3. **data-model.md** ✅
   - Skill entity definitions (full YAML specs for all 3 skills)
   - Catalog structure (discovery paths, dependencies)
   - Validation model (gate types, automation)
   - Entity relationships
   - Constraints (folder immutability, metadata sync, example testing)
   - Future extension points (skills v2, tagging, variants, telemetry)

4. **quickstart.md** ✅
   - 30-second TL;DR
   - How to find skills (CATALOG.md, Copilot chat, README.md)
   - How to understand a skill (file structure walkthrough)
   - Step-by-step workflow (5 phases)
   - Full example (Order entity with all 3 skills)
   - Common paths (read-only, CRUD, logic-only, new endpoint)
   - Real-world troubleshooting FAQs
   - Maintenance guide for skill authors

5. **Contracts** ✅ (in `contracts/` directory)
   - **skill-schema.json**: JSON schema for validating metadata.json files
   - **catalog-api.yaml**: OpenAPI spec for future tool/portal integration
   - **validation-checklist-schema.json**: CI automation schema for validation gates

---

## 🎓 Constitution Compliance

**All 7 core principles verified ✅**:

| Principle                    | How Skills Enforce It                                                                  |
| ---------------------------- | -------------------------------------------------------------------------------------- |
| **Vertical Slice**           | Each skill covers one layer-zone; collectively they teach full vertical slice assembly |
| **Layer Boundaries**         | Success criteria explicitly prohibit (DO/DON'T) layer boundary violations              |
| **Class-First Domain**       | Domain Modeling + CRUD Skills mandate entity encapsulation + mutation methods          |
| **EF Core Configuration**    | Domain Modeling Skill uses ProfileMapper auto-config pattern from AGENTS.md            |
| **Event-Driven Integration** | CRUD Skill includes EventPublisher.Publish() in workflow                               |
| **Test-First Quality**       | All skills include test templates + must pass validation gates                         |
| **Code-Verified Patterns**   | All examples reference ProfileV1Endpoint + CustomerProfile (canonical examples)        |

---

## 📊 Success Criteria Mapping

| Success Criterion                                   | How Plan Achieves It                                                         |
| --------------------------------------------------- | ---------------------------------------------------------------------------- |
| **SC-001**: 90% of pilots find skill in <2 min      | CATALOG.md searchable table + README.md quick navigation + @skills command   |
| **SC-002**: 85% of artifacts pass first review      | Skill templates enforce AGENTS.md patterns; examples prove templates work    |
| **SC-003**: 25% reduction in rework                 | Consistent skill workflow ensures artifact structure completeness            |
| **SC-004**: Add new skill in <30 min                | CONVENTIONS.md documents skill creation rules; template provided             |
| **SC-005**: 100% of published skills pass checklist | CI validation script enforces metadata schema + example tests + completeness |

---

## 🔄 Recommended Workflows

### Workflow 1: New Full-Stack Feature (120 min)
1. Domain Modeling Skill (20-30 min) → Create Order entity + mapping
2. CRUD Operations Skill (45-60 min) → Implement Create/Update commands + events
3. API Endpoints Skill (30-40 min) → Wire HTTP routes + OpenAPI docs

### Workflow 2: Read-Only Entity (70 min)
1. Domain Modeling Skill (20-30 min) → Create ReportData entity
2. API Endpoints Skill (30-40 min) → Expose as queryable endpoints
- *Skip CRUD: No mutations needed*

### Workflow 3: Business Logic Update (45 min)
1. CRUD Operations Skill (45-60 min) → Add/modify commands + validation
- *Skip Domain Modeling: Entity already mapped*
- *Skip API Endpoints: Routes already exist*

---

## 🛠️ Implementation Guidance (Phase 2)

The following tasks remain for Phase 2 (run `/speckit.tasks` command):

### Task Category 1: Skill Content Creation (~3 weeks)
- [ ] Create domain-modeling/skill.md (step-by-step guide)
- [ ] Create domain-modeling/templates/*.cs (mapper, entity templates)
- [ ] Create domain-modeling/examples/customer-profile-example/ (worked example)
- [ ] Repeat for crud-operations/ and api-endpoints/ skills
- [ ] Write domain-modeling/checklist.md (and repeat for other skills)
- [ ] Create domain-modeling/metadata.json (and repeat)

### Task Category 2: Catalog & Discovery (~1 week)
- [ ] Create .github/skills/README.md (quick-start guide)
- [ ] Create .github/skills/CATALOG.md (searchable index)
- [ ] Create .github/skills/CONVENTIONS.md (maintenance rules)
- [ ] Implement generate-catalog.sh (auto-index from metadata.json)
- [ ] Create .github/workflows/validate-skills.yaml (CI validation)

### Task Category 3: Testing & Validation (~1 week)
- [ ] Create Minimal.App.Tests/Skills/DomainModelingSkillTests.cs
- [ ] Create Minimal.App.Tests/Skills/CrudOperationsSkillTests.cs
- [ ] Create Minimal.App.Tests/Skills/ApiEndpointsSkillTests.cs
- [ ] Wire example tests into skill validation CI
- [ ] Test skill workflows with pilot developers

### Task Category 4: Documentation & Training (~1 week)
- [ ] Create skill author guide (CONVENTIONS.md + examples)
- [ ] Create troubleshooting guide in quickstart.md
- [ ] Record video walkthroughs for each skill (optional, Phase 2+)
- [ ] Update root README.md with link to skills
- [ ] Update AGENTS.md with "How to Use Skills" section

### Task Category 5: Integration & Tooling (optional, Phase 2+)
- [ ] Implement Copilot agent for `@skills` chat command
- [ ] Create skills-config.json auto-generation in CI
- [ ] Implement optional skills portal/dashboard (future)
- [ ] Add telemetry for skill usage analytics

---

## 📈 Success Metrics

Once implementation is complete, measure success via:

1. **Discoverability** (SC-001)
   - Test: New developer picks a skill in <2 minutes
   - Target: 90% of pilot developers succeed

2. **Artifact Quality** (SC-002)
   - Measure: % of PRs using skills that pass first review without rework
   - Target: ≥85% first-pass acceptance

3. **Rework Reduction** (SC-003)
   - Measure: Rework requests on feature PRs before/after skills launch
   - Target: 25% reduction within one release cycle

4. **Maintainability** (SC-004)
   - Test: Skill author adds 4th skill following CONVENTIONS.md
   - Target: <30 minutes to add new skill end-to-end

5. **Compliance** (SC-005)
   - Measure: % of published skills passing validation checklist
   - Target: 100% (CI gate prevents non-compliant skills from merging)

---

## 📝 Related Documents

- **[plan.md](plan.md)** — Complete implementation plan (this document's full version)
- **[spec.md](spec.md)** — Original feature specification + user stories
- **[research.md](research.md)** — Phase 0 research findings + design decisions
- **[data-model.md](data-model.md)** — Skill entity definitions + catalog structure
- **[quickstart.md](quickstart.md)** — Developer onboarding + usage guide
- **[contracts/](contracts/)** — Design artifacts (schema, API spec, validation rules)
- **[AGENTS.md](../../AGENTS.md)** — DKNet.Templates architecture reference
- **[constitution.md](../../.specify/memory/constitution.md)** — Project governance + principles

---

## 🚀 Next Steps

1. **Review this plan** with the team (code review, architecture review)
2. **Approve design** (verify no blockers, all assumptions validated)
3. **Run `/speckit.tasks` command** to generate Phase 2 implementation task breakdown
4. **Begin Phase 2 implementation** (assign tasks, track progress)
5. **Pilot with developers** (test with actual feature development)
6. **Iterate** (collect feedback, refine skills based on real usage)
7. **Publish & promote** (socialize skills, encourage adoption)

---

## ✨ Summary

**All planning complete.** Design is:
- ✅ **Aligned with DKNet.Templates constitution** (all 7 principles verified)
- ✅ **Achievable** (no blocking unknowns; clear implementation path)
- ✅ **Sustainable** (CONVENTIONS.md + CI validation + documentation framework)
- ✅ **Measurable** (SC-001 through SC-005 all testable)
- ✅ **Extensible** (clear path to 50+ skills beyond MVP of 3)

**No showstoppers identified.** Ready for Phase 2 implementation.

---

**Plan Created**: 2026-03-17  
**Branch**: `001-copilot-skills-pack`  
**Planning Duration**: ~4 hours  
**Next Command**: `/speckit.tasks` → Generate Phase 2 task breakdown
