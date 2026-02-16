# GitHub Copilot Skills for DKNet Templates

## Overview
This Skills folder contains comprehensive guides for developing applications using the DKNet template and packages. These guides are designed to help GitHub Copilot provide better code suggestions and assist developers in following best practices.

## What are GitHub Copilot Skills?
GitHub Copilot skills are documentation files that help Copilot understand your codebase patterns, conventions, and best practices. By maintaining these skills, you help Copilot generate more accurate and context-aware code suggestions.

## Skill Files

### 1. [Project Structure and Architecture](./01-project-structure-and-architecture.md)
Learn about the overall project organization, layer responsibilities, and architectural patterns.

**Topics Covered**:
- Clean Architecture layers (Domains, Infra, AppServices, Api)
- Feature-based organization
- Dependency flow and relationships
- Naming conventions
- Vertical slice architecture

**Use when**: Setting up a new project or understanding how components interact.

---

### 2. [EfCore Domain Entity Development](./02-efcore-domain-entity-development.md)
Master the creation of rich domain entities with proper encapsulation and business logic.

**Topics Covered**:
- AggregateRoot base class usage
- Constructor patterns (public vs internal)
- Property encapsulation with private setters
- Domain events
- Repository interfaces
- Best practices for entity design

**Use when**: Creating new domain entities or refactoring existing ones.

---

### 3. [EfCore Configuration Development](./03-efcore-configuration-development.md)
Learn how to configure EF Core entity mappings using DKNet conventions.

**Topics Covered**:
- DefaultEntityTypeConfiguration usage
- Property constraints and indexes
- Table and schema mapping
- Relationships configuration
- Auto-configuration setup
- Data seeding patterns

**Use when**: Configuring database mappings or creating migrations.

---

### 4. [Action/Command Development](./04-action-command-development.md)
Understand how to implement commands (write operations) using the CQRS pattern.

**Topics Covered**:
- Command structure and immutability
- FluentValidation integration
- Command handlers with IHandler<TCommand, TResult>
- Create, Update, Delete patterns
- Result pattern usage
- Domain event emission
- Mapping with Mapster

**Use when**: Implementing create, update, or delete operations.

---

### 5. [Query Development](./05-query-development.md)
Learn how to implement queries (read operations) with optimized data retrieval.

**Topics Covered**:
- Query structure with IWitResponse
- Result DTOs and projection
- Single item, list, and paginated queries
- Repository query methods
- Query optimization techniques
- Filter builder patterns

**Use when**: Implementing data retrieval operations.

---

### 6. [API Configuration and Endpoint Development](./06-api-configuration-endpoint-development.md)
Master API endpoint configuration using minimal APIs with versioning.

**Topics Covered**:
- Endpoint configuration with IEndpointConfig
- HTTP method mappings (GET, POST, PUT, DELETE)
- API versioning strategies
- Idempotency filters
- OpenAPI/Swagger documentation
- Authorization and rate limiting

**Use when**: Creating or modifying API endpoints.

---

### 7. [Repository Pattern Implementation](./07-repository-pattern-implementation.md)
Learn how to implement and use repositories for data access.

**Topics Covered**:
- IReadRepository and IWriteRepository interfaces
- Custom repository interfaces and implementations
- Generic repository usage
- Common repository patterns
- Service registration
- Query projection

**Use when**: Implementing data access logic or creating custom repositories.

---

### 8. [Validation and Mapping Patterns](./08-validation-and-mapping-patterns.md)
Master validation and mapping strategies for clean data flow.

**Topics Covered**:
- Data Annotations for basic validation
- FluentValidation for complex rules
- Handler validation for database checks
- Mapster mapping with attributes
- Custom mapping configurations
- Lazy mapping patterns

**Use when**: Implementing validation logic or configuring object mappings.

---

## How to Use These Skills

### For Developers
1. **Read Before Coding**: Review the relevant skill before implementing a feature
2. **Reference During Development**: Keep skills open for quick reference
3. **Follow Patterns**: Use the examples as templates for your implementations
4. **Ask Copilot**: Reference specific skills in your Copilot prompts

### For GitHub Copilot
These files help Copilot understand:
- Project structure and conventions
- Naming patterns and code organization
- DKNet package usage patterns
- Best practices and anti-patterns

### Example Copilot Prompts
```
// Reference a specific skill
"Create a new domain entity following the patterns in 02-efcore-domain-entity-development.md"

// Combine multiple skills
"Create a complete CRUD feature for Product entity including:
- Domain entity (skill 02)
- EF Core configuration (skill 03)
- Commands and handlers (skill 04)
- Query handlers (skill 05)
- API endpoints (skill 06)"

// Ask for specific patterns
"Implement a paginated query using the pattern from skill 05"
```

## Quick Reference

### Creating a New Feature
Follow this order:
1. **Domain Layer** (Skills 02, 07):
   - Create entity in `Domains/Features/{Feature}/Entities/`
   - Define repository interface in `Domains/Features/{Feature}/Repos/`

2. **Infrastructure Layer** (Skills 03, 07):
   - Create EF Core mapper in `Infra/Features/{Feature}/Mappers/`
   - Implement repository in `Infra/Features/{Feature}/Repos/`

3. **Application Layer** (Skills 04, 05, 08):
   - Create commands in `AppServices/{Feature}/V1/Actions/`
   - Create queries in `AppServices/{Feature}/V1/Queries/`
   - Add validators and mapping configurations

4. **Presentation Layer** (Skill 06):
   - Create endpoint config in `Api/ApiEndpoints/{Feature}Endpoints.cs`

### Common Patterns Quick Links

| Pattern | Skill File | Section |
|---------|-----------|---------|
| Create Command | 04 | Create Command |
| Update Command | 04 | Update Command |
| Delete Command | 04 | Delete Command |
| Single Query | 05 | Single Item Query |
| List Query | 05 | List Query |
| Paginated Query | 05 | Paginated Query |
| Entity with Events | 02 | Domain Events |
| Custom Repository | 07 | Custom Repository Interface |
| FluentValidation | 08 | FluentValidation |
| Custom Mapping | 08 | Custom Mapping Configuration |

## DKNet Package Reference

### Core Packages
- **DKNet.EfCore.Abstractions**: Base entity classes and interfaces
- **DKNet.EfCore.Repos**: Generic repository implementations
- **DKNet.EfCore.Events**: Domain event publishing
- **DKNet.AspCore.SlimBus**: API integration
- **DKNet.SlimBus.Extensions**: CQRS patterns and handlers

### Key Types
- `AggregateRoot`: Base class for domain entities
- `IReadRepository<T>`: Read-only repository interface
- `IWriteRepository<T>`: Full CRUD repository interface
- `IWitResponse<T>`: Query/Command response marker (Fluents)
- `IHandler<TRequest, TResponse>`: Request handler interface
- `DefaultEntityTypeConfiguration<T>`: EF Core configuration base

## Maintenance

### Updating Skills
When you make changes to the codebase that introduce new patterns or best practices:
1. Update the relevant skill file
2. Add examples from your actual code
3. Update this README if you add new skills
4. Commit skills with your code changes

### Adding New Skills
Create new skill files for:
- New architectural patterns
- Integration with new libraries
- Complex cross-cutting concerns
- Team-specific conventions

### Naming Convention
Use this format: `{number}-{topic-name}.md`
- Example: `09-security-best-practices.md`

## Best Practices

1. **Keep Skills Updated**: Update skills when patterns change
2. **Use Real Examples**: Include actual code from your project
3. **Be Specific**: Provide concrete examples, not just theory
4. **Cross-Reference**: Link related skills together
5. **Explain Why**: Don't just show how, explain why patterns exist
6. **Include Anti-Patterns**: Show what NOT to do
7. **Version Aware**: Note which .NET/package versions patterns apply to

## Contributing

When adding or updating skills:
1. Follow the existing format and structure
2. Include code examples
3. Explain best practices and reasoning
4. Update this README with links to new skills
5. Test examples to ensure they work

## Resources

### External Documentation
- [DKNet Packages](https://www.nuget.org/profiles/baoduy) - Official NuGet packages
- [Entity Framework Core](https://docs.microsoft.com/ef/core/) - EF Core documentation
- [ASP.NET Core](https://docs.microsoft.com/aspnet/core/) - ASP.NET Core documentation
- [FluentValidation](https://docs.fluentvalidation.net/) - Validation library
- [Mapster](https://github.com/MapsterMapper/Mapster) - Mapping library

### Related Files
- [README.md](../README.md) - Project README
- [AZURE_APP_CONFIG_ASPIRE_INTEGRATION.md](../AZURE_APP_CONFIG_ASPIRE_INTEGRATION.md) - Azure integration guide

## Support

For questions or issues:
1. Check the relevant skill file first
2. Review the main project README
3. Consult DKNet package documentation
4. Ask your team or create an issue

---

**Last Updated**: 2026-02-16

These skills are designed to grow with your project. Keep them updated and they'll help your team maintain consistency and quality.
