# {FeatureName}

> {One sentence: what this feature manages/does.}

## What Is This?

{2–4 sentences describing the feature. What entity does it manage? What lifecycle does it support?
What key behaviors does it provide? Who are the consumers of this feature?}

## Why Does It Exist?

{The business problem this feature solves. List 2–4 bullet points explaining:
- What it enables
- What compliance or workflow it supports
- How it relates to other features}

## Quick Start

### {Most common operation, e.g., Create a {Entity}}

```http
POST /api/v1/{feature-route}
Content-Type: application/json
Authorization: Bearer {token}

{
  "field1": "value",
  "field2": "value"
}
```

### Get a {Entity}

```http
GET /api/v1/{feature-route}/{id}
Authorization: Bearer {token}
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| **{ConceptName}** | {Brief explanation of what it is and why it matters} |
| **{ConceptName}** | {Brief explanation} |
| **Status** | Lifecycle state: `{State1} → {State2} / {State3}` |
| **Soft Delete** | Records are never hard-deleted; `IsDeleted = true` marks them inactive |

## Feature Map

> Source files for this feature across all layers.

| Layer | Path |
|-------|------|
| Domain Entity | `Minimal.Domains/Features/{EntityFolder}/Entities/{EntityName}.cs` |
| EF Core Mapper | `Minimal.Infra/Features/{EntityFolder}/Mappers/{EntityName}Mapper.cs` |
| Create Handler | `Minimal.AppServices/{FeatureFolder}/V1/Actions/Create.cs` |
| Update Handler | `Minimal.AppServices/{FeatureFolder}/V1/Actions/Update.cs` |
| Delete Handler | `Minimal.AppServices/{FeatureFolder}/V1/Actions/Delete.cs` |
| Domain Events | `Minimal.AppServices/{FeatureFolder}/V1/Events/` |
| Query Specs | `Minimal.AppServices/{FeatureFolder}/V1/Specs/` |
| API Endpoints | `Minimal.Api/ApiEndpoints/{EntityName}V1Endpoints.cs` |

## Related Documentation

- [Architecture](./architecture.md)
- [API Reference](./api-reference.md)
- [Data Model](./data-model.md)
- [Domain Events](./events.md)
