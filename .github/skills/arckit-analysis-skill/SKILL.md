---
name: arckit-analysis-skill
description: "Deeply analyze live features end-to-end to produce comprehensive technical documentation, diagrams, and risk assessments. Traces execution paths, identifies components, data flows, external dependencies, and observability gaps."
metadata:
  category: architecture
  type: analysis
  complexity: high
  estimated-time: "2-4 hours per feature"
---

# arckit-analysis-skill

## Purpose

Deeply analyze a live feature end-to-end to produce comprehensive, implementation-ready technical documentation and diagrams. This skill extracts all aspects of how a feature currently works: orchestration, domain behavior, persistence, messaging, external integrations, observability, risks, and testing.

## When to Use

- You need to understand _how a feature **currently** works_ (not how to design a new one).
- You must document end-to-end flow for handoff to engineering, QA, SRE, security, or product teams.
- You're refactoring and need to understand edge cases, dependencies, and failure modes.
- You're auditing code for security, performance, scalability, or compliance impact.

## When NOT to Use

- **Design mode**: You're designing a new feature architecture (use arckit-design-skill instead).
- **Q&A mode**: You have a specific question about an existing design (use arckit-qa-skill instead).

## Scope

- **One feature at a time**.
- **Full lifecycle coverage**: entry points, orchestration, domain behavior, persistence, messaging/events, external integrations, observability, reliability, testing posture.
- **Trace execution**: handlers/services → domain → repositories → events.
- **Validate state transitions**: identify all status/state changes and triggers.
- **Evidence-first**: cite concrete code locations and actual behavior; mark assumptions explicitly.

## Process

### 1. Determine Feature Boundaries
- Identify the feature by path, endpoint, command/query, or domain concept.
- Collect existing artifacts: spec.md, plan.md, architecture.md, architecture-review.md, runbooks, dashboards.
- Ask clarifying questions if boundaries are ambiguous.

### 2. Locate Entry Points and Trace Paths
- **API layer**: Find minimal API endpoints or endpoint configs (Api/ApiEndpoints/).
- **Command/Query layer**: Locate handlers under AppServices/Features/<Feature>/.
- **Domain layer**: Find aggregate roots, domain events, specifications in Domains/.
- **Infra layer**: Map repositories, EF Core configs, migrations, external clients.
- **Event handlers**: Locate event handlers and their side effects.
- **Background jobs**: Find job implementations and triggers.

### 3. Document End-to-End Execution
For each entry point (endpoint, command, event handler, job):
1. Trace the handler logic: what does it call?
2. Find domain operations: what invariants does the aggregate enforce?
3. Map repository queries: what data is loaded, projected, modified?
4. Identify events: what domain events are emitted, when, and by whom?
5. Track async flows: external calls, messaging, delayed processing.
6. Note observability: logging, metrics, traces injected.
7. Identify edge cases: retry logic, fallbacks, error handling.

### 4. Extract Component Responsibility Map
For each major component (handler, service, repository, external client):
- **What does it own?** (inputs, outputs, invariants)
- **What does it depend on?** (injected services, external calls)
- **What can fail?** (network, validation, concurrency, quota)
- **How is it tested?** (unit, integration, end-to-end)

### 5. Document Data Model and Persistence Impact
- **Entities affected**: which aggregates or entities are read/written?
- **Consistency guarantees**: single transaction, eventual consistency, no consistency?
- **Query patterns**: projections, pagination, filtering, N+1 risks?
- **Migrations**: any schema changes, data seeding, triggers?

### 6. Assess API and Contract Surface
- **Input contracts**: DTOs, validation rules, null handling.
- **Output contracts**: serialization, field order, optional fields.
- **Error contracts**: HTTP status codes, error formats, error codes.
- **Versioning**: are there multiple API versions?

### 7. Identify Messaging, Events, and Async Processing
- **Domain events**: what events are emitted, when, by whom?
- **Event handlers**: what reacts to events, what side effects occur?
- **External messaging**: are messages sent to external systems?
- **Retry/backoff**: how are transient failures handled?
- **Dead-letter handling**: what happens to failed messages?

### 8. Trace External Dependencies and Failure Modes
- **External API calls**: which integrations, what happens if they fail?
- **Configuration**: what global state is required?
- **Time-based behavior**: scheduling, timeouts, rate limits?
- **Concurrency**: are there race conditions, locking, or isolation issues?

### 9. Assess Security, Compliance, and Data Sensitivity
- **Access control**: who can invoke this feature, based on what?
- **Data sensitivity**: what PII, financial, or regulated data flows through?
- **Encryption**: is sensitive data encrypted at rest or in transit?
- **Audit trail**: what actions are logged, by whom, what details?
- **Compliance**: GDPR, PCI-DSS, SOX, or other regulation compliance?

### 10. Review Observability, Metrics, and Alerting
- **Structured logging**: what key events are logged with context?
- **Metrics**: latency, throughput, error rates, queue depths?
- **Tracing**: are execution paths instrumented for distributed tracing?
- **Dashboards and alerts**: what operational dashboards exist?

### 11. Assess Performance and Scalability
- **Throughput**: expected message/request volume?
- **Latency**: acceptable response times, timeout values?
- **Resource usage**: CPU, memory, disk, network bandwidth?
- **Saturation**: connection pools, concurrent request limits, queue limits?
- **Bottlenecks**: CPU-bound, I/O-bound, or external-service-bound?
- **Auto-scaling**: does the infrastructure scale horizontally?

### 12. Review Testing Coverage and Gaps
- **Unit tests**: handlers, domain, validators, repositories?
- **Integration tests**: with real database, external mocks?
- **End-to-end tests**: full request flow, feature workflows?
- **Edge cases**: boundary values, error conditions, concurrency?
- **Test data**: fixtures, seeding, cleanup?

### 13. Surface Risks, Trade-offs, and Open Questions
- **Reliability risks**: single points of failure, cascading failures, timeout handling?
- **Security risks**: injection, privilege escalation, data exposure?
- **Privacy risks**: data retention, deletion, GDPR compliance?
- **Performance risks**: N+1 queries, synchronous external calls, lock contention?
- **Maintenance risks**: code complexity, test coverage, undocumented assumptions?
- **Trade-offs made**: simplified architecture vs. scalability, eventual vs. strong consistency?
- **Open questions**: unclear requirements, missing tests, undocumented behavior?

### 14. Generate Recommendations
- **Priorities**: impact (high/medium/low) × effort (small/medium/large).
- **Quick wins**: easy, high-impact improvements.
- **Strategic**: large refactors to address systemic issues.
- **Future**: post-launch improvements, scaling strategies.

## Output Artifacts

Create or update these files under `src/docs/<feature>/`:

### feature-e2e-analysis.md (15 Sections, ~3000–5000 words)

1. **Executive Summary** (100–200 words)
   - Feature at a glance: purpose, entry points, key components, confidence level.
   - Top risks and recommendations.

2. **Feature Scope and Boundaries**
   - Clear definition of what is/isn't included.
   - User journeys and entry points.

3. **Business and Technical Objectives**
   - Why the feature exists.
   - Success criteria and performance targets.

4. **End-to-End Flow Walkthrough**
   - Step-by-step execution from entry to completion, with alternate/error paths.
   - Code citations for each step.

5. **Components and Responsibilities**
   - Major handler, service, repository, external client, event handler roles.
   - Dependency map.

6. **Data Model and Persistence Impact**
   - Entities read/written, consistency model, query patterns, N+1 risks.
   - Migrations and schema impact.

7. **API and Contract Surface**
   - Input/output DTOs, validation, error handling, versioning.
   - Contract examples.

8. **Messaging, Events, and Async Processing**
   - Domain events, event handlers, external messaging, retry logic, dead-letter handling.
   - Sequence diagrams for async flows.

9. **External Dependencies and Failure Modes**
   - Third-party integrations, timeouts, fallbacks, failure cascades.
   - Resilience strategy.

10. **Security, Compliance, and Data Sensitivity**
    - Access control, PII/sensitive data handling, encryption, audit trail, regulatory compliance.

11. **Observability, Metrics, and Alerting**
    - Logging strategy, metrics collected, alerting thresholds, dashboards.

12. **Performance and Scalability Considerations**
    - Throughput, latency, resource utilization, bottlenecks, scaling strategy.

13. **Testing Coverage and Gaps**
    - Unit, integration, end-to-end, edge-case, test data.
    - Coverage percentage and what's missing.

14. **Risks, Trade-offs, and Open Questions**
    - Ranked by impact and effort.
    - Unresolved design questions.

15. **Implementation or Refactor Recommendations**
    - Prioritized action items with justification.

### feature-diagrams.md (Mermaid Diagrams)

Include with explanations:

1. **System Context Diagram**
   - Feature in relation to external systems, users, APIs.

2. **End-to-End Sequence Diagram**
   - Main happy path: actor → API → handler → domain → repo → response.
   - Show async flows and event handlers separately if complex.

3. **Data Flow Diagram**
   - Data movement through layers: inputs → processing → persistence → outputs.
   - Identify sensitive data flows.

4. **State/Status Transition Diagram** (if applicable)
   - All state/status enum values and what triggers transitions.
   - Guard conditions and invalid transitions.

5. **Failure/Retry Flow Diagram**
   - Error paths, retry logic, exponential backoff, dead-letter handling.

### architecture-decision-log.md (if new decisions identified)

- Capture decisions made during analysis.
- Rationale, alternatives considered, consequences.
- Link to code evidence.

## Quality Checklist

- ✅ **15 sections complete** in feature-e2e-analysis.md.
- ✅ **5 diagrams present** in feature-diagrams.md with clear explanations.
- ✅ **Evidence-first**: every major claim cites a code file and line.
- ✅ **No speculation**: assumptions are labeled "inferred" or "assumed".
- ✅ **Complete traceability**: entry point → handler → domain → repo → event → response.
- ✅ **Risk ranking**: each risk has impact and effort estimates.
- ✅ **Recommendations prioritized**: quick wins first, strategic second, future third.
- ✅ **Diagrams are readable**: split large flows into multiple diagrams.
- ✅ **Naming consistency**: code symbols match diagram labels.

## Hints and Patterns

### Tracing a Handler
```
Find ApiEndpoint → Routes to handler request → Inject deps → OnHandle() method
→ Call service/repo → Domain aggregate operation → Emit events → Return result
```

### Finding Event Handlers
```
grep -r "IEventHandler<YourEvent>" Mx.Pgw.AppServices/ → Found handlers under Features/*/EventHandlers/
```

### Checking for N+1 Queries
```
Search repo for .Include/.ThenInclude patterns and ProjectToType usage.
Missing includes = likely N+1.
```

### Identifying Async Boundaries
```
Public async methods in handlers, services, repos = I/O boundary.
Check for .Wait() or .Result = potential deadlock.
```

### Finding External Service Calls
```
Search for HttpClient, IDurianClient, ILauncxClient, etc.
Check for timeout, retry, and fallback handling.
```

## Hands-On Example

If analyzing the "Charge Creation" feature:

1. **Find entry point**: Api/ApiEndpoints/Charges/CreateChargeEndpoint.cs
2. **Find handler**: Features/Charges/Actions/CreateChargeHandler.cs
3. **Trace domain**: Domains/Charge/Charge.cs → aggregate methods called
4. **Trace persistence**: Infractions/Configurations/ChargeConfiguration.cs, repos used
5. **Find events**: Domain.Charge.Aggregate → AddEvent<ChargCreatedEvent>()
6. **Find handlers**: Features/Charges/EventHandlers/*
7. **Check external calls**: Services injected, client calls, timeouts
8. **Document orchestration**: diagram the full flow
9. **Assess risks**: payment failures, duplicate requests, audit trail

---

## Before You Start

Before running analysis, ensure:
- [ ] Feature path or name is clear
- [ ] Boundary between this feature and adjacent features is defined
- [ ] Existing spec.md, plan.md, architecture.md have been reviewed (if present)
- [ ] You have access to the codebase for citation

---

**End of arckit-analysis-skill**
