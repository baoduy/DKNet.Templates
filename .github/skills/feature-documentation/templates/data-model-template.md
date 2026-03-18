# {FeatureName} — Data Model

## Entity Relationship Diagram

```mermaid
erDiagram
    {ENTITY_TABLE_NAME} {
        uniqueidentifier Id PK "Auto-generated GUID"
        nvarchar(150)   Field1 "Not null"
        nvarchar(256)   Field2 UK "Unique, not null"
        nvarchar(50)    Field3 "Nullable"
        nvarchar(50)    Status "Enum: Pending / Approved / Rejected"
        bit             IsDeleted "Soft delete flag; default false"
        nvarchar(450)   CreatedBy FK "Linked to user"
        datetime2       CreatedAt "UTC, auto-set on insert"
        nvarchar(450)   UpdatedBy "Nullable"
        datetime2       UpdatedAt "UTC, auto-updated"
    }

    RELATED_TABLE {
        uniqueidentifier Id PK
        uniqueidentifier {EntityName}Id FK
        nvarchar(100)   SomeField
    }

    {ENTITY_TABLE_NAME} ||--o{ RELATED_TABLE : "has many"
```

> Remove the `RELATED_TABLE` block if there are no related entities.  
> Add `UK` (unique key) annotation to columns with unique indexes.

---

## Properties

| Property | C# Type | DB Column | Constraints |
|----------|---------|-----------|-------------|
| `Id` | `Guid` | `Id` (PK) | Not null, auto-generated |
| `Field1` | `string` | `Field1` | Not null, max {N} chars |
| `Field2` | `string` | `Field2` | Not null, max {N} chars, unique index |
| `Field3` | `string?` | `Field3` | Nullable, max {N} chars |
| `Status` | `string` | `Status` | Not null, max 50 chars |
| `IsDeleted` | `bool` | `IsDeleted` | Default: `false` |
| `CreatedBy` | `string` | `CreatedBy` | Not null (from `RequestBase.ByUser`) |
| `CreatedAt` | `DateTime` | `CreatedAt` | UTC, auto-set on insert |
| `UpdatedBy` | `string?` | `UpdatedBy` | Nullable |
| `UpdatedAt` | `DateTime?` | `UpdatedAt` | UTC, auto-updated |

---

## EF Core Mapping Configuration

Source: `SlimBus.Infra/Features/{EntityFolder}/Mappers/{EntityName}Mapper.cs`

Key mapping decisions:

| Configuration | Value | Reason |
|--------------|-------|--------|
| Table name | `{EntityTableName}` (schema: `dbo`) | Convention |
| Unique index | `{Field2}` | Business uniqueness constraint |
| Unique index | `{Field1}` (if applicable) | {Reason} |
| Global query filter | `IsDeleted == false` | Automatically excludes soft-deleted records from all queries |
| Column type | `datetime2(7)` for `UpdatedAt` | Sub-second precision for audit trail |
| Column precision | `nvarchar({N})` for `Field1` | Max length constraint from business rules |

---

## Validation Rules

| Field | Rule | Enforcement |
|-------|------|-------------|
| `Field2` (e.g., Email) | Must be unique per entity | DB unique index + FluentValidation Spec check before insert |
| `Field1` (e.g., Name) | 2–{N} characters | FluentValidation in `Create{EntityName}RequestValidator` |
| `Field3` (e.g., Phone) | Valid phone format if provided | FluentValidation with `.When()` conditional |
| `Status` | Valid transition only (`Pending → Approved/Rejected`) | Domain entity method enforces valid transitions |
| Soft delete | `IsDeleted` flag | EF Core Global Query Filter excludes deleted from all queries |

---

## Status Values

> Remove this section if the entity has no status field.

| Value | Meaning | Transitions to |
|-------|---------|----------------|
| `Pending` | Default on create; awaiting review | `Approved`, `Rejected` |
| `Approved` | Passed review; active | N/A (terminal for workflow) |
| `Rejected` | Failed review; inactive | N/A (terminal for workflow) |

---

## Migration History

| Migration Name | Description |
|----------------|-------------|
| `Initial_{EntityName}` | Create initial `{EntityTableName}` table |
| `Add_{Field}_To_{EntityName}` | {Reason for the change} |

> Keep this table updated when running `./add-migration.sh <Name>`.
