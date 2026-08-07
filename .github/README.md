# Agent Relationship Map (Arckit + DKNet)

This document maps `arckit` and `dknet` agents with separate diagrams per agent.

## arckit.feature-architect

```mermaid
---
config:
  theme: mc
  layout: elk
---
flowchart LR
  A[arckit.feature-architect]
  W[arckit.feature-worker]
  S1[arckit-analysis-skill]
  S2[arckit-design-skill]
  S3[arckit-qa-skill]
  E[Explore]
  C[dotnet-concurrency-specialist]
  P[dotnet-performance-analyst]

  A -->|delegates per feature| W
  A -->|analysis mode uses| S1
  A -->|design mode uses| S2
  A -->|Q&A mode uses| S3
  A --> E
  A --> C
  A --> P

  classDef agent fill:#1d4ed8,stroke:#93c5fd,color:#ffffff,stroke-width:2px;
  classDef subagent fill:#065f46,stroke:#6ee7b7,color:#ffffff,stroke-width:2px;
  classDef skill fill:#6d28d9,stroke:#c4b5fd,color:#ffffff,stroke-width:2px;
  classDef mcp fill:#9a3412,stroke:#fdba74,color:#ffffff,stroke-width:2px;

  class A agent;
  class W,E,C,P subagent;
  class S1,S2,S3 skill;
```

## arckit.code-knowledge-qa

```mermaid
---
config:
  theme: mc
  layout: elk
---
flowchart LR
  CK[arckit.code-knowledge-qa]
  Q[arckit.qdrant-docsearch-qa]
  F[arckit.falkordb-codegraph-qa]
  QS[qdrant-docsearch skill]
  QM[MCP: qdrant-docsearch/*]
  FS[falkordb-codegraph skill]
  FM[MCP: falkordb-codegraph/*]

  CK -->|documentation evidence path| Q
  CK -->|code relationship evidence path| F
  Q -->|uses skill| QS
  Q -->|uses MCP tools| QM
  F -->|uses skill| FS
  F -->|uses MCP tools| FM

  classDef agent fill:#1d4ed8,stroke:#93c5fd,color:#ffffff,stroke-width:2px;
  classDef subagent fill:#065f46,stroke:#6ee7b7,color:#ffffff,stroke-width:2px;
  classDef skill fill:#6d28d9,stroke:#c4b5fd,color:#ffffff,stroke-width:2px;
  classDef mcp fill:#9a3412,stroke:#fdba74,color:#ffffff,stroke-width:2px;

  class CK agent;
  class Q,F subagent;
  class QS,FS skill;
  class QM,FM mcp;
```

## dknet.developer

```mermaid
---
config:
  theme: mc
  layout: elk
---
flowchart LR
  D[dknet.developer]
  SP1[speckit.specify]
  SP2[speckit.clarify]
  SP3[speckit.plan]
  SP4[speckit.checklist]
  SP5[speckit.tasks]
  SP6[speckit.analyze]
  SP7[speckit.implement]
  A[arckit.feature-architect]
  U[dknet-unit-test]

  D -->|phase 1| SP1
  D -->|phase 2| SP2
  D -->|phase 3| SP3
  D -->|phase 5| SP4
  D -->|phase 6| SP5
  D -->|phase 7| SP6
  D -->|phase 8| SP7
  D -->|phase 4 design docs| A
  D -->|phase 10 feature docs| A
  D -->|phase 9 testing skill| U

  classDef agent fill:#1d4ed8,stroke:#93c5fd,color:#ffffff,stroke-width:2px;
  classDef subagent fill:#065f46,stroke:#6ee7b7,color:#ffffff,stroke-width:2px;
  classDef skill fill:#6d28d9,stroke:#c4b5fd,color:#ffffff,stroke-width:2px;
  classDef mcp fill:#9a3412,stroke:#fdba74,color:#ffffff,stroke-width:2px;

  class D agent;
  class SP1,SP2,SP3,SP4,SP5,SP6,SP7,A subagent;
  class U skill;
```

## dknet-bdd-test

```mermaid
---
config:
  theme: mc
  layout: elk
---
flowchart LR
  B[dknet-bdd-test]
  S[dknet-bdd-tests]
  CL[dknet-bdd-tests/checklist.md]

  B -->|required skill| S
  B -->|completion gate| CL

  classDef agent fill:#1d4ed8,stroke:#93c5fd,color:#ffffff,stroke-width:2px;
  classDef subagent fill:#065f46,stroke:#6ee7b7,color:#ffffff,stroke-width:2px;
  classDef skill fill:#6d28d9,stroke:#c4b5fd,color:#ffffff,stroke-width:2px;
  classDef mcp fill:#9a3412,stroke:#fdba74,color:#ffffff,stroke-width:2px;

  class B agent;
  class S,CL skill;
```

## dknet.efcore-config

```mermaid
---
config:
  theme: mc
  layout: elk
---
flowchart LR
  E[dknet.efcore-config]
  S[dknet-efcore-config skill]

  E -->|required skill loading| S

  classDef agent fill:#1d4ed8,stroke:#93c5fd,color:#ffffff,stroke-width:2px;
  classDef subagent fill:#065f46,stroke:#6ee7b7,color:#ffffff,stroke-width:2px;
  classDef skill fill:#6d28d9,stroke:#c4b5fd,color:#ffffff,stroke-width:2px;
  classDef mcp fill:#9a3412,stroke:#fdba74,color:#ffffff,stroke-width:2px;

  class E agent;
  class S skill;
```

## Notes

- `dknet.developer` orchestrates Spec-Kit phases and delegates architecture phases to `arckit.feature-architect`.
- `arckit.feature-architect` delegates per-feature execution to `arckit.feature-worker` and applies `arckit-analysis-skill`, `arckit-design-skill`, and `arckit-qa-skill`.
- `arckit.code-knowledge-qa` combines documentation-intent and code-relationship evidence paths through `arckit.qdrant-docsearch-qa` and `arckit.falkordb-codegraph-qa`.
- MCP-backed workers are `arckit.qdrant-docsearch-qa` (`qdrant-docsearch/*`) and `arckit.falkordb-codegraph-qa` (`falkordb-codegraph/*`), with skills `qdrant-docsearch` and `falkordb-codegraph`.
- `dknet-bdd-test` explicitly requires the `dknet-bdd-tests` skill and its checklist gate.
- `dknet.efcore-config` explicitly requires the `dknet-efcore-config` skill.

## Future Update Requirements

- Keep a consistent color system across all Mermaid diagrams:
  - agent = blue
  - subagent = green
  - skill = purple
  - mcp = orange
- Keep standalone diagram sections for user-invocable agents.
- Include sub-agent relationships inside parent diagrams where relevant.
- For any sub-agent shown, include linked skills and MCP tool names when available.
- Keep `dknet.efcore-config` linked to `dknet-efcore-config skill`.
- Do not label nodes with "internal sub-agent" wording in diagram labels.
