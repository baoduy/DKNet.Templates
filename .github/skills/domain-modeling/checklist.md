# Validation Checklist: Domain Modeling with EFCore Mapping

**Purpose**: Verify your domain entity and mapper are configured correctly  
**Skill**: [Domain Modeling Skill](./skill.md)  
**Estimated Review Time**: 5 minutes

---

## ✅ Entity Class Checklist

| #   | Requirement                                       | Success Looks Like                                                            | Fix If Failed                                                  |
| --- | ------------------------------------------------- | ----------------------------------------------------------------------------- | -------------------------------------------------------------- |
| 1   | Entity class created in correct folder            | File at `src/SlimBus.Domains/Features/<YourFeature>/Entities/<YourEntity>.cs` | Move file to correct location and update namespace             |
| 2   | Class is sealed                                   | `public sealed class YourEntity`                                              | Add `sealed` keyword to class declaration                      |
| 3   | Properties use required modifier where applicable | Properties marked with `required` keyword for non-optional fields             | Add `required` modifier or init-only property `{ get; init; }` |
| 4   | Entity has ID property                            | `public required Guid Id { get; init; }`                                      | Add Id property for entity identity                            |
| 5   | Has Create() factory method                       | Static method that validates inputs and returns new instance                  | Add factory method with validation logic                       |
| 6   | Has encapsulated mutation methods                 | Methods like `Update()` that modify state                                     | Add mutation methods instead of exposing setters               |

---

## ✅ Mapper Configuration Checklist

| #   | Requirement                            | Success Looks Like                                                               | Fix If Failed                                                    |
| --- | -------------------------------------- | -------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| 7   | Mapper created in Mappers folder       | File at `src/SlimBus.Infra/Features/<YourFeature>/Mappers/<YourEntity>Mapper.cs` | Move file to correct location under `.../Mappers/`               |
| 8   | Mapper class is sealed                 | `public sealed class YourEntityMapper : IEntityTypeConfiguration<YourEntity>`    | Add `sealed` keyword; Scrutor auto-registration requires it      |
| 9   | Implements IEntityTypeConfiguration<T> | `public class YourEntityMapper : IEntityTypeConfiguration<YourEntity>`           | Inherit from `IEntityTypeConfiguration<>` interface              |
| 10  | All properties configured              | Each entity property has a `builder.Property()` configuration call               | Add ConfigureProperty calls for every property                   |
| 11  | String lengths enforced                | `builder.Property(x => x.Email).HasMaxLength(256)`                               | Add `.HasMaxLength()` for all string properties                  |
| 12  | Required fields marked as required     | `builder.Property(x => x.Name).IsRequired()`                                     | Mark required properties; mark optional with `IsRequired(false)` |

---

## ✅ Relationships & Indexes Checklist

| #   | Requirement                                | Success Looks Like                                                            | Fix If Failed                                              |
| --- | ------------------------------------------ | ----------------------------------------------------------------------------- | ---------------------------------------------------------- |
| 13  | Foreign keys configured                    | `builder.HasOne(...).WithMany(...).HasForeignKey(...)` if relationships exist | Configure relationships with `.HasOne()` and `.WithMany()` |
| 14  | Indexes created for query patterns         | `builder.HasIndex(x => x.UserId).HasDatabaseName("IX_...")`                   | Add indexes for columns used in WHERE clauses or joins     |
| 15  | Composite indexes for multi-column queries | `builder.HasIndex(x => new { x.UserId, x.Email })` if needed                  | Add composite indexes for multi-column query patterns      |

---

## ✅ Migration & Database Checklist

| #   | Requirement                            | Success Looks Like                                        | Fix If Failed                                                                         |
| --- | -------------------------------------- | --------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| 16  | Migration generated successfully       | Run `./add-migration.sh YourMigrationName` with no errors | Verify mapper is sealed and in correct folder; try again                              |
| 17  | Migration applies cleanly              | Run `dotnet ef database update` with no errors            | Review migration SQL; fix any conflicts or constraints                                |
| 18  | Database schema matches configuration  | Table created with correct column names, types, lengths   | Run `SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='YourTable'` to verify |
| 19  | Primary key constraint applied         | Table has PRIMARY KEY constraint on Id column             | Verify `builder.HasKey(x => x.Id)` in mapper                                          |
| 20  | Unique constraints applied (if needed) | Email has UNIQUE constraint if marked as unique in mapper | Add `.IsUnique()` to index configuration                                              |

---

## ✅ Code Quality Checklist

| #   | Requirement                        | Success Looks Like                                                               | Fix If Failed                                                |
| --- | ---------------------------------- | -------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| 21  | Code compiles without errors       | Run `dotnet build src/DKNet.Templates.sln -c Release` — zero errors              | Fix compilation errors; typically namespace or syntax issues |
| 22  | Code compiles with zero warnings   | Same build command produces zero warnings                                        | Address all warnings; project enforces warnings-as-errors    |
| 23  | XML documentation comments         | All non-obvious properties and methods have `/// <summary>` comments             | Add doc comments for clarity                                 |
| 24  | Namespace matches folder path      | `namespace SlimBus.Domains.Features.YourFeature.Entities`                        | Update namespace to match file location                      |
| 25  | Follows project naming conventions | Entity names PascalCase, property names PascalCase, mapper names end in `Mapper` | Rename to match conventions                                  |

---

## 📋 Quick Checkbox Version

Copy this into your PR checklist:

- [ ] 1: Entity class in correct folder
- [ ] 2: Class is sealed
- [ ] 3: Properties use required modifier
- [ ] 4: Has ID property
- [ ] 5: Has Create() factory method
- [ ] 6: Has mutation methods
- [ ] 7: Mapper in Mappers folder
- [ ] 8: Mapper is sealed
- [ ] 9: Implements IEntityTypeConfiguration<T>
- [ ] 10: All properties configured
- [ ] 11: String lengths enforced
- [ ] 12: Required fields marked
- [ ] 13: Foreign keys configured
- [ ] 14: Indexes for queries
- [ ] 15: Composite indexes if needed
- [ ] 16: Migration generated
- [ ] 17: Migration applies cleanly
- [ ] 18: Database schema matches
- [ ] 19: Primary key constraint
- [ ] 20: Unique constraints (if needed)
- [ ] 21: Compiles without errors
- [ ] 22: Compiles with zero warnings
- [ ] 23: Has doc comments
- [ ] 24: Namespace correct
- [ ] 25: Follows naming conventions

---

## 🔍 How to Review This Checklist

1. **Go through items 1–15**: Ensure entity and mapper structure is correct
2. **Go through items 16–20**: Verify migration and database integration
3. **Go through items 21–25**: Ensure code quality standards are met
4. **Self-check all boxes**: If any item is unchecked, refer to "Fix If Failed" guidance
5. **If still stuck**: See [Common Errors & Fixes](./skill.md#common-errors--how-to-fix-them) in skill.md

---

## ✨ All Items Complete? You're Done!

Once all 25 items pass:

1. ✅ You've successfully completed the Domain Modeling Skill
2. ✅ Your entity is ready for business logic (see [CRUD Operations Skill](../crud-operations/skill.md))
3. ✅ Request code review (if in a PR)

**Next**: Follow the [CRUD Operations Skill](../crud-operations/skill.md) to add business logic and commands.

---

**Skill Version**: 1.0.0 | **Last Updated**: 2026-03-17
