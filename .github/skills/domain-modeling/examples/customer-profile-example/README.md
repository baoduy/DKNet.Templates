# Domain Modeling Example: CustomerProfile

This example demonstrates the complete Domain Modeling Skill applied to a real, production-ready entity: `CustomerProfile`.

---

## Files in This Example

### 1. **CustomerProfile.cs** — Domain Entity

The domain entity class that represents a customer profile in the business domain.

**Key Patterns**:
- **Sealed class**: Required for Scrutor auto-registration via mapper
- **init-only properties**: Immutable after creation (except UpdatedAt); enforced by `{ get; init; }` syntax
- **required keyword**: Marks mandatory fields at compile time
- **Factory method** (`Create()`): Centralizes instantiation logic; single way to create entities
- **Encapsulated mutations** (`Update()` method): Business rules enforced here, not exposed directly
- **Timestamps**: CreatedAt (immutable) and UpdatedAt (mutable on changes)

**Quote from AGENTS.md - Class-First Domain Design**:
> Domain entities are classes with encapsulated state and mutation methods—never anemic data classes.
> Business rules live as methods on entities (`Update()`, `Validate()`, etc.), not in procedural services.

---

### 2. **CustomerProfileMapper.cs** — EF Core Mapper

The persistence mapping that configures how CustomerProfile maps to the database schema.

**Key Patterns**:
- **Sealed class**: Required for Scrutor auto-discovery (placed in `Mappers/` folder)
- **IEntityTypeConfiguration<T>**: Standard EF Core configuration pattern
- **Fluent API**: All configuration in `Configure()` method (no scattered attributes)
- **Property mapping**: Each property explicitly configured with constraints
- **String lengths**: MaxLength enforced at database level (prevents data truncation)
- **Unique constraints**: Email index marked as unique (database enforces uniqueness)
- **Composite indexes**: Multi-column indexes for common query patterns
- **Schema organization**: Related entities grouped in "customers" schema

**Quote from AGENTS.md - EF Core Auto Configuration**:
> All EF model configuration is declarative and centralized using `UseAutoConfigModel` and automatic
> mapper discovery from `Minimal.Infra/Features/<Feature>/Mappers`.

---

## How to Use This Example

### Copy & Customize Strategy

1. **Copy both files** to your feature folder:
   ```bash
   # Copy entity
   cp CustomerProfile.cs ../src/Minimal.Domains/Features/YourFeature/Entities/YourEntity.cs
   
   # Copy mapper
   cp CustomerProfileMapper.cs ../src/Minimal.Infra/Features/YourFeature/Mappers/YourEntityMapper.cs
   ```

2. **Rename the classes**:
   - Replace `CustomerProfile` with your entity name (e.g., `Order`, `Invoice`)
   - Replace `CustomerProfileMapper` with `YourEntityMapper`

3. **Update properties to match your domain**:
   - Change `FullName`, `Email`, `DateOfBirth` to your properties
   - Keep factory method pattern (`Create()`)
   - Keep mutation method pattern (`Update()`)
   - Keep audit timestamps (`CreatedAt`, `UpdatedAt`)

4. **Update namespace**:
   - Entity: `Minimal.Domains.Features.YourFeature.Entities`
   - Mapper: `Minimal.Infra.Features.YourFeature.Mappers`

5. **Update database configuration**:
   - Change schema name if appropriate (e.g., `"customers"` → `"orders"`)
   - Change table name (e.g., `"CustomerProfiles"` → `"Orders"`)
   - Adjust indexes based on your query patterns

---

## Key Design Decisions Explained

### Why sealed?
The `sealed` keyword prevents inheritance and enables Scrutor auto-discovery. Together, these enforce
a deterministic, predictable object model where each entity is self-contained.

### Why init-only properties?
`init` properties (set only during initialization) enforce immutability after creation. This prevents
accidental state changes through direct assignment. State changes go through encapsulated methods (`Update()`)
where business rules can be enforced.

### Why factory method?
`Create()` is the single point of instantiation. Every CustomerProfile goes through this method, ensuring:
- All required fields are provided
- ID is always generated uniquely
- Timestamps are always set correctly
- Business validation rules (if any) are enforced once

### Why mutation method?
`Update()` is the encapsulated way to modify the profile. By funneling all mutations through this method,
we ensure:
- UpdatedAt timestamp is always maintained
- Business rules are enforced before changes take effect
- Audit trail is preserved (all modifications tracked)

### Why so many indexes?
Indexes are configured based on **how the entity is queried**:
- `IX_CustomerProfiles_UserId`: "Find all profiles for a user" (common read)
- `IX_CustomerProfiles_Email`: "Find profile by email" + uniqueness constraint
- `IX_CustomerProfiles_CreatedAt`: "Find new profiles in date range"
- `IX_CustomerProfiles_UserId_CreatedAt`: "Paginate a user's profiles by creation date"

---

## Relationship to AGENTS.md

This example follows all patterns documented in AGENTS.md:

| AGENTS.md Section                  | Pattern                                  | Evidence in Example                                    |
| ---------------------------------- | ---------------------------------------- | ------------------------------------------------------ |
| **Feature Vertical Slice Pattern** | Entity lives in layer-appropriate folder | `Minimal.Domains/Features/.../Entities/`               |
| **Strict Layer Boundaries**        | Business logic stays in domain           | Mutations in `Update()` method, not in infra           |
| **Class-First Domain Design**      | Sealed class with encapsulation          | `sealed` keyword, `init` properties, `Update()` method |
| **EF Core Auto Configuration**     | Mapper in Mappers/ folder, sealed        | `Minimal.Infra/Features/.../Mappers/`                  |
| **No scattered configuration**     | Fluent API in Configure() method         | All EF config in one place                             |

---

## Testing This Example

### Compile & Verify
```bash
cd /Users/steven/_CODE/GIT/DKNet.Templates
dotnet build src/DKNet.Templates.sln -c Release
# Expected: Zero errors, zero warnings ✅
```

### Run migrations
```bash
cd src/Minimal.ApiEndpoints
./add-migration.sh CustomerProfileInitial
dotnet ef database update
```

### Verify database schema
Check that the CustomerProfiles table was created with correct columns, types, and indexes.

---

## Next Steps

Once you understand this example, follow the workflow:

1. **CRUD Operations** — Add business logic (commands, handlers, events)
2. **API Endpoints** — Expose via REST endpoints with DTOs

---

**Example Version**: 1.0.0 | **Last Updated**: 2026-03-17 | **Status**: Production-Ready
