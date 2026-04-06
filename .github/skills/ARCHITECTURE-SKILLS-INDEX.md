# Architecture Skills Index

This directory contains reusable skills that support the unified `arckit.feature-architect` agent. Each skill handles a specific behavior: analysis, design, or Q&A.

## Quick Navigation

| Skill | Purpose | When to Use | Output Artifacts |
|---|---|---|---|
| [arckit-analysis-skill](./arckit-analysis-skill/SKILL.md) | Analyze existing feature end-to-end | "Analyze [feature] flow, risks, observability, testing" | feature-e2e-analysis.md, feature-diagrams.md, architecture-decision-log.md |
| [arckit-design-skill](./arckit-design-skill/SKILL.md) | Design new feature architecture | "Design architecture for [new feature]" | specs/<feature>/architecture.md, architecture-review.md |
| [arckit-qa-skill](./arckit-qa-skill/SKILL.md) | Answer questions about existing architecture | "Why/How/Where is [design decision]?" | Direct answer with evidence and gaps |

## How They Work Together

### Mode Detection
The unified agent `arckit.feature-architect` automatically detects which skill to use:

```
User Intent                          Detected Mode          Skill Used
─────────────────────────────────────────────────────────────────────
"Analyze feature X"                  Analysis               arckit-analysis-skill
"Design architecture for X"          Design                 arckit-design-skill
"Why is X designed that way?"        Q&A                    arckit-qa-skill
Ambiguous                            (asks clarifying Q)    (none yet)
```

### No Manual Switching
You don't need to manually invoke individual skills. Use the unified agent and it routes automatically:

**Example**:
```
/arckit.feature-architect "Analyze Charge creation end-to-end"
→ (agent detects analysis intent → uses arckit-analysis-skill)
```

## Skill Descriptions

### arckit-analysis-skill
**Tracing existing features down to the code level.**

- **Input**: Feature name/path, optional existing artifacts
- **Process**: Trace handlers → domain → repos → events; extract flow diagrams, components, risks
- **Output**: 
  - `src/docs/<feature>/feature-e2e-analysis.md` (15 sections)
  - `src/docs/<feature>/feature-diagrams.md` (5 Mermaid diagrams)
  - `src/docs/<feature>/architecture-decision-log.md` (if new decisions found)
- **Audience**: Engineers needing to understand current behavior, refactoring, auditing
- **Time**: ~2–4 hours manual work per feature (skill does it in ~30 min with AI)

### arckit-design-skill
**Designing class-first OOP architecture before implementation.**

- **Input**: `specs/<feature>/spec.md`, `specs/<feature>/plan.md`, optional research/data-model
- **Process**: Map to repo patterns, propose components/interfaces, DI structure, async boundaries, security, testing
- **Output**:
  - `specs/<feature>/architecture.md` (14 sections, implementation-ready)
  - `specs/<feature>/architecture-review.md` (validation checklist)
- **Audience**: Engineers designing new features, architects validating decisions
- **Time**: ~1–2 hours manual work per feature (skill does it in ~20 min)

### arckit-qa-skill
**Answering specific architecture questions using documented design + code evidence.**

- **Input**: Feature name + specific architecture question
- **Process**: Consult feature-e2e-analysis.md → diagrams → architecture.md → plan.md → code; assemble answer with evidence
- **Output**: Focused answer with source citations, gaps, confidence level, recommendations
- **Audience**: Engineers curious about design decisions, architecture reviews, refactoring planning
- **Time**: Real-time, ~2–5 min per question

## Integration with Other Agents/Workflow

```
speckit.specify → speckit.plan → [arckit-design-skill]
                                      ↓
                              arckit.feature-architect
                                      ↓
              [arckit-analysis-skill] ← (if existing feature)
                      ↓
                [arckit-qa-skill] ← (if questions arise)
                      ↓
              speckit.tasks
                      ↓
            speckit.implement
```

## Calling the Unified Agent

### From Command Line / Chat
```bash
/arckit.feature-architect "Analyze Payout submission flow and document risks"
/arckit.feature-architect "Design architecture for customer profile export with RBAC"
/arckit.feature-architect "For Charge settlement, why is payment reconciliation event-driven?"
```

### From AGENTS.md
```markdown
- Use [arckit.feature-architect](/agents/arckit.feature-architect.agent.md) for all architecture work (analysis, design, Q&A).
```

## Skill File Structure

Each skill directory contains:
- `SKILL.md` — Comprehensive guidance (process, quality checklist, patterns, examples)
- (Optional) Additional resources or templates

Example:
```
arckit-analysis-skill/
├── SKILL.md                 ← Full guidance for analysis workflow
├── README.md               ← (optional) Quick reference
└── examples/               ← (optional) Sample outputs
    ├── charge-analysis.md
    └── payout-diagrams.md
```

## Quality Gates for Each Skill

### arckit-analysis-skill
✅ **15 sections complete** in feature-e2e-analysis.md  
✅ **5 diagrams present** in feature-diagrams.md  
✅ **Evidence-first**: every major claim cites code location  
✅ **No speculation**: assumptions labeled as "inferred"  
✅ **Risks ranked**: impact × effort matrix  

### arckit-design-skill
✅ **14 sections complete** in architecture.md  
✅ **Architecture review pass**: all validation checks  
✅ **Component naming**: follows repository conventions  
✅ **DI clearly specified**: constructor injection, lifetimes  
✅ **Async boundaries explicit**: all I/O marked, timeouts/retries specified  

### arckit-qa-skill
✅ **Direct answer provided** with evidence citations  
✅ **Source priority respected**: docs consulted first, code second  
✅ **Confidence level stated**: High/Medium/Low/Requires Precursor  
✅ **Gaps surfaced**: if docs missing or stale, said explicitly  
✅ **Recommendation offered**: next action if needed  

## Migration from Old Agents

**Old agents → New consolidated agent:**

| Old Agent | Equivalent | How to Use New Agent |
|---|---|---|
| `arckit.architecture-qa` | Q&A mode | `/arckit.feature-architect` + question about existing feature |
| `arckit.architecture` | Design mode | `/arckit.feature-architect` + "Design architecture for..." |
| `arckit.feature-e2e-analyst` | Analysis mode | `/arckit.feature-architect` + "Analyze ... end-to-end" |

See [CONSOLIDATION-MIGRATION-GUIDE.md](./CONSOLIDATION-MIGRATION-GUIDE.md) for details.

## Examples

### Analyze a Feature
```
/arckit.feature-architect "Analyze the Charge creation feature end-to-end. Document flow, 
components, data model, external dependencies, failure modes, security, observability, 
and risks. Produce feature-e2e-analysis.md, feature-diagrams.md, and recommendations."
```

### Design New Architecture
```
/arckit.feature-architect "Design the architecture for a new 'Customer Profile Export' 
feature. Requirements: RBAC access control, full data export to CSV, audit logging. 
Produce architecture.md with class-first OOP design and architecture-review.md validation."
```

### Answer Architecture Questions
```
/arckit.feature-architect "For the Settlement feature:
1. Why is settlement status updated asynchronously?
2. How do settlement retries work?
3. What risks exist if the external provider times out?"
```

## Best Practices

1. **Be specific about feature scope**: "Charge creation" not just "Charge"
2. **Provide context if available**: Link to spec.md, plan.md, or existing docs
3. **Ask one skill at a time**: "Analyze X" then later "Design Y" rather than both in one prompt
4. **Reference existing artifacts**: "Update feature-e2e-analysis.md with latest refactoring" is clearer than "re-analyze"
5. **For Q&A, be precise**: "Why event-driven?" is better than "How does it work?"

## Troubleshooting

### "I asked for analysis but got design instead"
The agent may have detected "Design" intent. Rephrase with "Analyze [feature] to document flow and produce feature-e2e-analysis.md."

### "The output is incomplete or missing sections"
Check the quality checklist for the relevant skill (15 sections for analysis, 14 for design). Tell the agent explicitly: "Ensure all X sections are complete."

### "I need both current and recommended architecture"
Ask for analysis first ("current state"), then design ("recommended architecture"). The agent will maintain both contexts.

### "The skill doesn't know about feature X"
Features without existing docs may have lower confidence answers. The agent will flag missing artifacts and recommend running analysis first.

## Contributing

To add new patterns or improve existing skills:

1. Update the relevant `SKILL.md` file with new sections, examples, or quality gates
2. Add concrete examples from the Monxa codebase
3. Update the skills index and migration guide if structure changes
4. Test with sample features to ensure guidance is actionable

---

**Quick Start**: Use `/arckit.feature-architect` for all architecture work. It will automatically route to the right skill.

---

End of Architecture Skills Index.
