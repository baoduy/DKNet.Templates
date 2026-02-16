# Project Structure and Architecture

## Overview
This project follows Clean Architecture principles with Domain-Driven Design (DDD) patterns, implementing CQRS (Command Query Responsibility Segregation) for application logic.

## Layer Structure

### 1. **{ProjectName}.Domains** - Domain Layer
- **Purpose**: Core business logic, entities, and domain contracts
- **Dependencies**: DKNet.EfCore.Abstractions, DKNet.EfCore.Repos.Abstractions
- **Contains**:
  - `Features/{FeatureName}/Entities/` - Domain entities and aggregate roots
  - `Features/{FeatureName}/Repos/` - Repository interfaces
  - `Share/` - Shared domain constants and schemas

**Key Patterns**:
- Entities inherit from `AggregateRoot` (from DKNet.EfCore.Abstractions)
- Use private setters for properties to enforce encapsulation
- Domain events are added via `entity.AddEvent(new DomainEvent(...))`
- Constructor patterns: public for creation, internal for rehydration

### 2. **{ProjectName}.Infra** - Infrastructure Layer
- **Purpose**: Data persistence, EF Core configurations, external integrations
- **Dependencies**: DKNet.EfCore.Repos, DKNet.EfCore.Events, DKNet.EfCore.Relational.Helpers
- **Contains**:
  - `Features/{FeatureName}/Mappers/` - EF Core entity configurations
  - `Features/{FeatureName}/Repos/` - Repository implementations
  - `Features/{FeatureName}/StaticData/` - Seed data
  - `Features/{FeatureName}/ExternalEvents/` - External event handlers
  - `Data/` - DbContext and migrations

**Key Patterns**:
- Mappers inherit from `DefaultEntityTypeConfiguration<TEntity>`
- Use `UseAutoConfigModel()` for automatic entity configuration discovery
- Repository implementations inherit from DKNet generic repositories

### 3. **{ProjectName}.AppServices** - Application Layer
- **Purpose**: Business use cases, commands, queries, and handlers
- **Dependencies**: DKNet.SlimBus.Extensions for CQRS patterns
- **Contains**:
  - `{FeatureName}/V{Version}/Actions/` - Commands (Create, Update, Delete)
  - `{FeatureName}/V{Version}/Queries/` - Queries and result DTOs
  - `{FeatureName}/V{Version}/Events/` - Domain event handlers
  - `{FeatureName}/V{Version}/Validators/` - FluentValidation validators

**Key Patterns**:
- Commands implement `IWitResponse<TResult>` from Fluents.Requests
- Queries implement `IWitResponse<TResult>` from Fluents.Queries
- Handlers are internal and auto-registered
- Version-based folder structure (V1, V2, etc.)

### 4. **{ProjectName}.Api** - Presentation Layer
- **Purpose**: HTTP endpoints using minimal APIs
- **Dependencies**: DKNet.AspCore.SlimBus
- **Contains**:
  - `ApiEndpoints/` - Endpoint configuration classes
  - `Configs/` - Middleware, filters, and API configurations

**Key Patterns**:
- Endpoint classes implement `IEndpointConfig`
- Support for multiple API versions (V1, V2)
- Use extension methods: `MapPost`, `MapGet`, `MapPut`, `MapDelete`

### 5. **{ProjectName}.Share** - Shared Layer
- **Purpose**: Cross-cutting concerns and constants
- **Contains**: Configuration constants, shared utilities

### 6. **{ProjectName}.AppHost** - Application Host
- **Purpose**: Startup and service registration
- **Contains**: Program.cs with dependency injection setup

## Project Naming Convention
Use the pattern: `{CompanyName}.{ApplicationName}.{LayerName}`

Example:
- `SlimBus.Domains`
- `SlimBus.Infra`
- `SlimBus.AppServices`
- `SlimBus.Api`

## Feature Organization
Features are organized in a vertical slice architecture:
```
Features/{FeatureName}/
  Entities/         (Domains layer)
  Repos/           (Domains layer - interfaces, Infra layer - implementations)
  Mappers/         (Infra layer)
  StaticData/      (Infra layer)
  V1/              (AppServices layer)
    Actions/
    Queries/
    Events/
```

## Dependency Flow
```
Api → AppServices → Domains ← Infra
         ↓                      ↓
      Share ←――――――――――――――――――――
```

- **Api** depends on AppServices
- **AppServices** depends on Domains and Share
- **Infra** depends on Domains and Share
- **Domains** has no dependencies on other layers (only DKNet abstractions)
