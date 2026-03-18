---
name: dknet-domain-modeling
description: Create domain entities with EF Core mappers and proper validation. Use this when adding new database entities to the application.
---

# Skill: Domain Modeling with EFCore Mapping Configuration

**Duration**: 20–30 minutes | **Difficulty**: Intermediate | **Category**: Persistence & Entities

---

## Overview

**When to use this skill**: You're adding a new database entity to the application and need to configure it with EF Core mapping following the DKNet.Templates auto-configuration pattern.

**What you'll create**: A domain entity class and its corresponding EF Core mapper class that automatically integrates with the persistence layer.

**Context**: This is the foundational skill for the feature delivery workflow. It's always the first step when building a new feature. See [AGENTS.md - Feature Vertical Slice Pattern](../../../AGENTS.md) for more.

---

## Prerequisites: Do You Know This?

Before starting, ensure you have:

- [ ] Read [AGENTS.md - Feature Vertical Slice Pattern](../../../AGENTS.md#feature-vertical-slice-pattern-copy-this)
- [ ] Familiarity with C# classes, properties, and constructors
- [ ] Understanding of database relationships (one-to-many, foreign keys)
- [ ] Basic knowledge of Entity Framework Core concepts
- [ ] Access to VS Code and the `src/DKNet.Templates.sln`

If you don't have these, take 10 minutes to review them first — this skill builds on these foundations.

---

## Inputs Checklist: Gather This Information First

Before you start following the step-by-step workflow, collect this information:

- [ ] **Entity Name** (PascalCase): e.g., `CustomerProfile`, `Order`, `Invoice`
  - This is the domain concept you're modeling
  
- [ ] **List of Properties**: Name, C# Type, Required/Optional
  - Example: `FullName (string, required)`, `DateOfBirth (DateTime?, optional)`, `Email (string, required)`

- [ ] **Relationships** (if any): What other entities does this relate to?
  - Example: "Many CustomerProfiles belong to one User"

- [ ] **Validation Rules**: Min/max lengths, numeric ranges, custom rules
  - Example: "Email max length 256 characters", "FullName max 200 characters"

- [ ] **Query Patterns**: What are the most common database queries for this entity?
  - Example: "Find by UserId", "Find by Email", "Paginate by CreatedAt"

*Don't make up this information as you go — gather it first. It will make the workflow 10x smoother.*

---

## Step-by-Step Workflow

### Step 1: Create the Domain Entity Class

**What you're doing**: Define the business entity as a C# class in the Domains layer (no infrastructure concerns).

1. In VS Code, open `src/SlimBus.Domains/Features/` and create a new folder for your feature (e.g., `CustomerProfiles/`)
2. Inside that folder, create a `Entities/` subfolder
3. Create a new C# file: `<YourEntity>.cs` (e.g., `CustomerProfile.cs`)
4. Copy the [entity-template.cs](./templates/entity-template.cs) and customize it with your entity name and properties:

```csharp
// Example: CustomerProfile.cs
namespace SlimBus.Domains.Features.CustomerProfiles.Entities;

/// <summary>
/// Customer profile domain entity containing customer information.
/// Manages encapsulated state and mutations.
/// </summary>
public sealed class CustomerProfile
{
    /// <summary>
    /// Unique identifier for this customer profile.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Associated user ID (foreign key).
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Customer's full name.
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>
    /// Customer's email address.
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Customer's date of birth (optional).
    /// </summary>
    public DateTime? DateOfBirth { get; init; }

    /// <summary>
    /// Timestamp when profile was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when profile was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Factory method to create a new customer profile.
    /// </summary>
    public static CustomerProfile Create(
        Guid userId,
        string fullName,
        string email,
        DateTime? dateOfBirth = null)
    {
        return new CustomerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = fullName,
            Email = email,
            DateOfBirth = dateOfBirth,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Update customer profile information.
    /// Mutation method ensures encapsulation of state changes.
    /// </summary>
    public void Update(string fullName, string email, DateTime? dateOfBirth = null)
    {
        FullName = fullName;
        Email = email;
        DateOfBirth = dateOfBirth;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

**Expected output**: File compiles without errors (warnings checked later in validation).

**Common mistake**: Forgetting the `sealed` keyword or `required` keyword on properties.  
**Fix**: Review the template above; it includes required keywords per DKNet.Templates standards.

---

### Step 2: Create the EF Core Mapper Class

**What you're doing**: Define how the entity maps to the database schema using EF Core's fluent configuration API.

1. In `src/SlimBus.Infra/Features/`, create a folder matching your feature (e.g., `CustomerProfiles/`)
2. Inside that folder, create a `Mappers/` subfolder
3. Create a new mapper file: `<YourEntity>Mapper.cs` (e.g., `CustomerProfileMapper.cs`)
4. Copy the [mapper-template.cs](./templates/mapper-template.cs) and customize:

```csharp
// Example: CustomerProfileMapper.cs
using SmartCode.Domains.Features.CustomerProfiles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SlimBus.Infra.Features.CustomerProfiles.Mappers;

/// <summary>
/// EF Core mapper configuration for CustomerProfile entity.
/// Auto-discovered by Scrutor and applied via UseAutoConfigModel.
/// </summary>
public sealed class CustomerProfileMapper : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        // Table setup
        builder.ToTable("CustomerProfiles", "profiles");

        // Primary key
        builder.HasKey(x => x.Id);

        // Properties
        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.DateOfBirth)
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_CustomerProfiles_UserId");

        builder.HasIndex(x => x.Email)
            .HasDatabaseName("IX_CustomerProfiles_Email")
            .IsUnique();

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("IX_CustomerProfiles_CreatedAt");
    }
}
```

**Expected output**: Mapper file compiles; uses `sealed` keyword; inherits from `IEntityTypeConfiguration<T>`.

**Common mistake**: Adding business logic in the mapper or not placing it in the correct `Mappers/` folder.  
**Fix**: Mappers are configuration-only. Business logic belongs in domain entity methods. Folder placement matters for Scrutor auto-discovery.

---

### Step 3: Run Entity Framework Migration

**What you're doing**: Generate the database migration script and verify the schema is correct.

1. Open terminal in `src/SlimBus.ApiEndpoints/` directory
2. Run the migration script:
   ```bash
   ./add-migration.sh CustomerProfileInitial
   ```
3. Verify the generated migration file in `src/SlimBus.Infra/Migrations/` looks correct:
   - Table name matches your mapper setup
   - Columns and constraints match your properties
   - Indexes are created correctly

4. Run the migration to create the database schema:
   ```bash
   dotnet ef database update
   ```

**Expected output**: Migration applies cleanly; database table created with correct schema.

**Common mistake**: Running migration before mapper is in correct location (Scrutor auto-discovery failure).  
**Fix**: Ensure mapper is in `SlimBus.Infra/Features/<Feature>/Mappers/` and is `sealed`.

---

### Step 4: Validate Your Work

Before you consider this skill complete, run the **validation checklist** below and ensure all items pass.

Run this command to double-check compilation:
```bash
cd /Users/steven/_CODE/GIT/DKNet.Templates
dotnet build src/DKNet.Templates.sln -c Release
```

Expected: Zero warnings, all projects build successfully ✅

---

## Success Validation: Checklist

Print or copy the checklist from [checklist.md](./checklist.md) and verify ALL items are complete:

- [ ] Entity class created in `SlimBus.Domains/Features/<Feature>/Entities/<Entity>.cs`
- [ ] Mapper class created in `SlimBus.Infra/Features/<Feature>/Mappers/<Entity>Mapper.cs`
- [ ] Mapper class is `sealed` (required for Scrutor auto-discovery)
- [ ] All entity properties mapped with correct EF configuration
- [ ] Validation rules enforced (string lengths, nullability, etc.)
- [ ] Indexes configured for common query patterns
- [ ] Migration script generates without errors
- [ ] Migration applies successfully to database
- [ ] Code compiles with zero warnings
- [ ] Related unit tests pass (if test path provided)

**If any item fails**: Refer to [checklist.md](./checklist.md) for detailed remediation.

---

## Common Errors & How to Fix Them

### Error: "Mapper class not auto-discovered (duplicate key exception or mapping configuration not applied)"

**Why it happens**: Mapper is either in wrong folder or not sealed; Scrutor cannot auto-register it.

**How to fix**:
1. Verify mapper is in: `SlimBus.Infra/Features/<Feature>/Mappers/`
2. Add `sealed` keyword to class declaration
3. Verify it inherits from `IEntityTypeConfiguration<YourEntity>`
4. Run migration again; it should now pick up the mapper

**Prevention**: Always use the [mapper-template.cs](./templates/mapper-template.cs); it has correct structure.

---

### Error: "Column or property '<PropertyName>' cannot be null (database constraint violation)"

**Why it happens**: Property is marked as required in mapper but is sometimes null in code.

**How to fix**:
1. Review the data: Is this property truly always present? If not, make it optional: `IsRequired(false)`
2. If it should be required, ensure your domain entity always provides a value
3. Run migration if you changed `IsRequired()` setting

---

### Error: "Type '<Type>' cannot be used as a column type"

**Why it happens**: You're using a C# type that EF Core doesn't support directly (e.g., `decimal?` without declaring precision).

**How to fix**:
1. Check EF Core [supported types documentation](https://docs.microsoft.com/en-us/ef/core/modeling/entity-properties#column-data-types)
2. For decimal type, configure precision: `builder.Property(x => x.Amount).HasPrecision(18, 2);`
3. For other types, ensure they're in the supported list or use a value converter

---

## Complete Working Example

See the files in `./examples/customer-profile-example/` for a complete, production-ready example:

- **[CustomerProfile.cs](./examples/customer-profile-example/CustomerProfile.cs)**: Full entity class with all properties and mutation methods explained
- **[CustomerProfileMapper.cs](./examples/customer-profile-example/CustomerProfileMapper.cs)**: Complete mapper with indexes, constraints, and inline comments
- **[README.md](./examples/customer-profile-example/README.md)**: Line-by-line explanation of every configuration choice

**Copy-paste strategy**:
1. Copy the entity file; rename class and properties for your domain entity
2. Copy the mapper file; rename mapper class and adapt property configurations
3. Update namespace to match your feature folder

---

## Next Steps: Continue the Feature Workflow

Once you've completed this skill, you're ready for:

1. **[CRUD Operations Skill](../crud-operations/skill.md)** (Next): Add business logic with commands and mutations
   - What it does: Create, Read, Update, Delete operations with command handlers and domain events
   - Why it's next: You've modeled the data; now add the behavior

2. **[API Endpoints Skill](../api-endpoints/skill.md)** (After CRUD): Expose your entity via REST API
   - What it does: Create API endpoints with DTOs and OpenAPI documentation
   - Why it's after: You need working CRUD operations before creating endpoints

---

## Questions or Issues?

- 📖 Review [CONVENTIONS.md](../CONVENTIONS.md) for project-wide rules
- 🔍 Search [CATALOG.md](../CATALOG.md) for related topics
- 🐛 Found a bug? Create an issue: `[SKILL] domain-modeling: <issue description>`
- 👥 Questions? Comment in PR or contact team maintainers

---

**Skill Version**: 1.0.0 | **Status**: Published | **Last Updated**: 2026-03-17
