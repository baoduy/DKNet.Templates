---
name: arckit-qa-skill
description: "Answer specific architecture questions about existing features using documented artifacts and code evidence. Consults analysis and design docs first, then reconciles with code when docs are incomplete or stale."
metadata:
  category: architecture
  type: qa
  complexity: medium
  estimated-time: "5-15 minutes per question"
---

# arckit-qa-skill

## Purpose

Answer specific architecture questions about existing features using documented architecture and code evidence. This skill consults feature analysis and design artifacts first, then reconciles with code when docs are incomplete or stale.

## When to Use

- You have a specific architecture question about an **existing feature** design.
- You want to understand _why_ a design decision exists, _where_ a responsibility belongs, or _how_ components interact.
- You're curious about edge cases, failure modes, risks, or trade-offs without needing full analysis docs.

## When NOT to Use

- **Analysis mode**: You need full end-to-end documentation of how a feature works (use arckit-analysis-skill).
- **Design mode**: You're designing architecture for a new feature (use arckit-design-skill).
- **General knowledge**: The question isn't about a specific feature's architecture (ask the default agent).

## Scope

- **One feature-specific question per turn** or one tightly related question set.
- **Current-state architecture focused**: how things are designed now, not how they should be redesigned.
- **Evidence-grounded**: cite documentation first, then code when needed to verify or fill gaps.
- **Frankness about gaps**: if required docs don't exist, say so and reduce confidence.

## Process

### 1. Interpret the Question
Break down what the user is asking:
- **Scope**: Single feature, cross-feature, or system-wide?
- **Level**: High-level design, component interaction, specific code path?
- **Type**: Why (rationale), How (mechanism), What (impact), Where (responsibility)?

### 2. Load Artifacts in Priority Order
Consult sources in order; stop when you have enough to answer:

1. **src/docs/<feature>/feature-e2e-analysis.md** (if exists)
   - Most comprehensive current-state documentation.
   - Contains flow, components, risks, recommendations.

2. **src/docs/<feature>/feature-diagrams.md** (if exists)
   - Sequence, data flow, state transition, failure diagrams.
   - Visual reference for complex interactions.

3. **src/docs/<feature>/architecture-decision-log.md** (if exists)
   - Explicit design decisions and rationale.
   - Alternatives considered and why they were rejected.

4. **specs/<feature>/architecture.md** (if exists)
   - Pre-implementation design document.
   - Component roles, interfaces, patterns.

5. **specs/<feature>/architecture-review.md** (if exists)
   - Architecture validation against .NET best practices.
   - Pass/fail checks and remediation notes.

6. **specs/<feature>/spec.md** (if exists)
   - Business requirements and problem statement.
   - Feature scope and objectives.

7. **specs/<feature>/plan.md** (if exists)
   - Design approach and high-level structure.
   - Key decisions and phases.

8. **AGENTS.md** and repository conventions
   - Baseline architecture patterns and layer boundaries.
   - Feature organization, naming, and standards.

9. **Code evidence** (as fallback/verification)
   - Actual implementation matching documented design.
   - Explicit behavior not captured in docs.

### 3. Answer with Confidence Level
- **High confidence**: Question answered from feature-e2e-analysis.md or architecture.md sections.
- **Medium confidence**: Question answered from code + repository patterns; docs not fully available.
- **Low confidence**: Question involves undocumented behavior or conflicting sources.

### 4. Surface Gaps and Drift
- **Missing artifact**: "This feature has no architecture.md; answer is inferred from code."
- **Outdated artifact**: "The feature-e2e-analysis.md is dated 3 months ago; implementation may have diverged."
- **Conflict**: "The spec says X, but code does Y."
- **Assumption**: "Documentation doesn't explain this; inferring from code pattern."

### 5. Recommend Next Actions
If the answer requires a precursor task:
- **Missing analysis**: "To fully understand this feature, run arckit-analysis-skill."
- **Outdated design**: "Architecture docs need refresh; consider running arckit-design-skill update mode."
- **Requires refactor**: "Current design has risks; consider running arckit.architecture-qa with refactor prompt."

## Answer Structure

Always return:

### 1. Feature or Subsystem (Context)
"For the Charge creation feature..." or "For the Settlement module..."

### 2. Direct Answer (the "Why"/"How"/"What"/"Where")
- **Short answer first**: 1–2 sentences.
- **Evidence**: cite document sections and line numbers, or code files and methods.
- **Examples or diagrams**: if helpful, embed Mermaid diagrams or code snippets.

### 3. Supporting Evidence
- Document citations: "Per feature-e2e-analysis.md §4 (End-to-End Flow), the handler calls..."
- Code citations: "See Mx.Pgw.AppServices/Features/Charges/Actions/CreateChargeHandler.cs line 45..."
- Cross-references: "This design decision is explained in architecture-decision-log.md under 'Why Event-Driven Settlement'."

### 4. Gaps, Conflicts, or Confidence Limits
- Missing docs: "No architecture.md exists for this feature; answer is inferred from code patterns."
- Stale docs: "The feature-e2e-analysis.md was written before recent refactors; confidence is medium."
- Unresolved questions: "The design docs don't explain how X and Y interact; this is an open gap."
- Code-doc conflict: "The plan.md says sync, but the handler uses async; implementation diverged."

### 5. Recommended Next Action (if needed)
- "To get detailed flow diagrams, run arckit-analysis-skill."
- "To review this design against best practices, run arckit-design-skill in review mode."
- "This design has known risks (see architecture.md §13); consider refactoring."

## Question Types and Patterns

### Pattern 1: "Why Specific Design?"
**Q**: "Why is charge status sync event-driven instead of synchronous?"

**Answer structure**:
1. Direct: "Settlement ownership is decoupled by design to handle asynchronous payment provider updates independently."
2. Evidence: "(1) Charge aggregate in Domains/Charge/Charge.cs emits ChargePaidEvent. (2) Features/Charges/EventHandlers/ChargePaidEventHandler.cs processes async. See architecture.md §8 (Async Reliability)."
3. Rationale: "Sync would block charge creation on settlement latency; event-driven allows charge to complete fast while settlement happens in background."

### Pattern 2: "How Do Components Interact?"
**Q**: "How does the OrderCancellation service interact with the Inventory system?"

**Answer structure**:
1. Direct: "OrderCancellation publishes a CancelledEvent; Inventory subsystem subscribes and decrements reserved stock."
2. Flow: "Handler → Domain.Cancel() → emits CancelledEvent → SaveChanges → IEventPublisher.PublishAsync() → RabbitMQ → Inventory listener."
3. Evidence: feature diagrams (Sequence Diagram) + code: Charge.Cancel(), Charge+Events.cs, EventHandlers/CancelledEventHandler.cs.

### Pattern 3: "Where Does Responsibility Live?"
**Q**: "Where is payment validation logic?"

**Answer structure**:
1. Scope: "Payment validation spans three places: (1) DTO validation (fluentvalidation in handler), (2) domain invariants (Charge aggregate), (3) external gateway checks (DurianClient)."
2. Layering: "Input validation → handler validator. Business rules → aggregate. External rules → gateway client. See architecture.md §5 (Component Design) and §9 (Security)."

### Pattern 4: "What Are the Risks?"
**Q**: "What happens if the payment provider takes 30 seconds to respond?"

**Answer structure**:
1. Current behavior: "The charge creation handler calls DurianClient.CreateAsync() with a 10-second timeout. If timeout, exception is caught, charge state reverts (see Charge.ReverseInitiation())."
2. Risks: "Timeout may leave payment pending in provider but failed locally → manual reconciliation required. See feature-e2e-analysis.md §9 (Failure Modes) and risks list."
3. Mitigation: "Implement idempotency keys so retries are safe. Add alerting for timeout threshold. See architecture-decision-log.md 'Payment Provider Timeout Strategy'."

## Confidence Levels

| Confidence | Criteria | When to Use |
|---|---|---|
| **High** | Question directly answered in feature-e2e-analysis.md or architecture.md | "The docs explain this clearly." |
| **Medium** | Question answered by combining code + repository patterns; docs incomplete | "Code shows X, which aligns with Y pattern." |
| **Low** | Question involves undocumented behavior or conflicting sources | "The spec says X but code does Y; confidence is low." |
| **Requires Precursor** | Answer requires missing analysis or design artifacts | "Need to run arckit-analysis-skill first." |

## Limitations

- ✋ **Do not** invent behavior or design decisions unsupported by documentation or code.
- ✋ **Do not** recommend refactoring unless explicitly asked (keep to current-state Q&A).
- ✋ **Do not** answer implementation questions ("How do I write the code?") — that's for the default agent.
- ✋ **Do not** broaden into cross-feature architecture without explicit scope (stick to one feature per turn).

## Example Session

**User**: "For the Payout feature, why are payouts retried 3 times instead of exponential backoff?"

**Agent** (arckit-qa-skill):

1. **Feature/Subsystem**: For the Payout submission and reconciliation feature.

2. **Direct Answer**: Payouts use fixed 3-retry strategy because payout providers (Durian, Launcx) don't guarantee idempotency. Exponential backoff could exceed provider payment windows.

3. **Evidence**:
   - Code: `Mx.Pgw.AppServices/Features/Payouts/Actions/SubmitPayoutHandler.cs` lines 78–92 (retry loop, 3 attempts max).
   - Config: `Mx.Pgw.Share/Infra/External/PayoutRetryPolicy.cs` (defines RetryCount = 3, DelayMs = 1000).
   - Docs: `specs/payout-submission/architecture.md` §8 (Reliability Strategy) explains the choice.

4. **Gaps/Confidence**: High confidence. The architecture.md clearly documents this decision. **Confidence: High**.
   - No conflict between docs and code.
   - Implementation matches design intent.

5. **Recommended Next**: If you want to reconsider the retry strategy, run arckit-design-skill in review mode to analyze trade-offs with exponential backoff.

---

## Before You Ask

Before asking an architecture question, ensure:
- [ ] The question is about a specific feature (not general .NET advice)
- [ ] You know the feature name or path (e.g., "Charge", "Payout")
- [ ] The question focuses on current-state design (not refactoring)
- [ ] You're aware that archived/legacy features may not have current docs

---

**End of arckit-qa-skill**
