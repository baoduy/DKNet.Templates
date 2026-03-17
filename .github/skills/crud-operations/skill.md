# Skill: CRUD Operations Implementation

**Duration**: 45–60 minutes | **Difficulty**: Intermediate | **Category**: Business Logic & Commands

---

## Overview

**When to use this skill**: Your domain entity (from Domain Modeling Skill) is ready for business logic. Add Create, Read, Update, Delete operations via commands and domain events.

**What you'll create**: Command classes, command handlers, repository interface/implementation, domain events, and validation specs.

**Context**: This is the business logic layer (AppServices + Infra). See [AGENTS.md - Commands, Mapping](../../../AGENTS.md#commands-mapping-and-user-context) for the pattern.

---

## Prerequisites: Do You Know This?

Before starting, ensure you have:

- [ ] Completed [Domain Modeling Skill](../domain-modeling/skill.md) for your entity
- [ ] Familiarity with command pattern (request → handler → response)
- [ ] Understanding of domain events and event sourcing concepts
- [ ] Knowledge of repository pattern for data access
- [ ] Comfort with async/await in C#

---

## Inputs Checklist: Gather This Information First

- [ ] **Entity name**: Which entity are you adding CRUD for? (e.g., `CustomerProfile`)
- [ ] **CRUD operations needed**: Create? Read? Update? Delete? (all typically yes, but confirm)
- [ ] **Business rules for mutations**: What validations/checks happen on Create/Update?
- [ ] **Query patterns**: How will this entity be fetched? By ID? By UserId? By Email?
- [ ] **Domain events**: What business events should be published? (e.g., `CustomerProfileCreatedEvent`)

---

## Step-by-Step Workflow

### Step 1: Create Request/Response DTOs in AppServices Layer

**What you're doing**: Define request/response DTOs with the `[GenerateDto]` attribute pattern. DTOs are auto-generated from domain entities using the code generator.

1. In `src/SlimBus.AppServices/Features/<YourFeature>/V1/` create DTO files
2. For request/response models, use the `[GenerateDto]` attribute:

```csharp
// Example: CustomerProfileDtos.cs
using DKNet.EfCore.DtoGenerator;
using Mapster;
using SlimBus.Domains.Features.CustomerProfiles.Entities;

namespace SlimBus.AppServices.Features.CustomerProfiles.V1;

// Response DTO - auto-generated from entity
[GenerateDto(typeof(CustomerProfile), Exclude = [])]
[MapsFrom(typeof(CustomerProfile))]
public sealed partial record CustomerProfileDto;

// Create/Update request DTOs
public sealed record CreateCustomerProfileRequest(
    string FullName,
    string Email,
    DateTime? DateOfBirth);

public sealed record UpdateCustomerProfileRequest(
    string FullName,
    string Email,
    DateTime? DateOfBirth);
```

**Expected**: Response DTOs use `[GenerateDto]` attribute; request DTOs are manual records with validators.

### Step 2: Create FluentValidation Validators for Each Request DTO

**What you're doing**: Create validators for every request DTO to enforce business rules at the boundary.

```csharp
using FluentValidation;
using SlimBus.Domains.Features.CustomerProfiles.Entities;

namespace SlimBus.AppServices.Features.CustomerProfiles.V1;

// Validator for Create request
public sealed class CreateCustomerProfileRequestValidator 
    : AbstractValidator<CreateCustomerProfileRequest>
{
    public CreateCustomerProfileRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required")
            .MaximumLength(200).WithMessage("Full name cannot exceed 200 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email format is invalid")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(DateTime.Today)
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("Date of birth must be in the past");
    }
}

// Validator for Update request
public sealed class UpdateCustomerProfileRequestValidator 
    : AbstractValidator<UpdateCustomerProfileRequest>
{
    public UpdateCustomerProfileRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required")
            .MaximumLength(200).WithMessage("Full name cannot exceed 200 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email format is invalid")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(DateTime.Today)
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("Date of birth must be in the past");
    }
}
```

**Expected**: One validator per request DTO, registered automatically by FluentValidation scanning.

### Step 3: Create Repository Interface in AppServices

**What you're doing**: Define the contract for data access. Implementation comes later in Infra.

```csharp
// In AppServices/Features/CustomerProfiles/Repositories/ICustomerProfileRepository.cs
namespace SlimBus.AppServices.Features.CustomerProfiles.Repositories;

using SlimBus.Domains.Features.CustomerProfiles.Entities;

public interface ICustomerProfileRepository
{
    Task<CustomerProfile?> GetByIdAsync(Guid id);
    Task<CustomerProfile?> GetByEmailAsync(string email);
    Task<List<CustomerProfile>> GetByUserIdAsync(Guid userId);
    Task AddAsync(CustomerProfile profile);
    Task UpdateAsync(CustomerProfile profile);
    Task DeleteAsync(Guid id);
    Task SaveChangesAsync();
}
```

### Step 3: Create Write Repository Interface in AppServices

**What you're doing**: Define the contract for **write operations only** (Create, Update, Delete). Read operations use Specs instead.

```csharp
// In AppServices/Features/CustomerProfiles/Repositories/ICustomerProfileRepository.cs
namespace SlimBus.AppServices.Features.CustomerProfiles.Repositories;

using SlimBus.Domains.Features.CustomerProfiles.Entities;

public interface ICustomerProfileRepository
{
    // Write operations only - Reads use Specs (see Step 2)
    Task AddAsync(CustomerProfile profile);
    Task UpdateAsync(CustomerProfile profile);
    Task DeleteAsync(Guid id);
    Task SaveChangesAsync();
}
```

**What you're doing**: Implement data access using EF Core context.

```csharp
// In Infra/Features/CustomerProfiles/Repos/CustomerProfileRepository.cs
namespace SlimBus.Infra.Features.CustomerProfiles.Repos;

using SlimBus.Domains.Features.CustomerProfiles.Entities;
using SlimBus.AppServices.Features.CustomerProfiles.Repositories;
using SlimBus.Infra.Contexts;

public sealed class CustomerProfileRepository : ICustomerProfileRepository
{
    private readonly ApplicationDbContext _context;

    public CustomerProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerProfile?> GetByIdAsync(Guid id)
        => await _context.CustomerProfiles.FirstOrDefaultAsync(x => x.Id == id);

    public async Task<CustomerProfile?> GetByEmailAsync(string email)
        => await _context.CustomerProfiles.FirstOrDefaultAsync(x => x.Email == email);

    public async Task<List<CustomerProfile>> GetByUserIdAsync(Guid userId)
        => await _context.CustomerProfiles.Where(x => x.UserId == userId).ToListAsync();

    public async Task AddAsync(CustomerProfile profile)
        => _context.CustomerProfiles.Add(profile);

    public async Task UpdateAsync(CustomerProfile profile)
        => _context.CustomerProfiles.Update(profile);

    public async Task DeleteAsync(Guid id)
    {
        var profile = await GetByIdAsync(id);
        if (profile != null)
            _context.CustomerProfiles.Remove(profile);
    }

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
```

### Step 4: Implement Write Repository in Infra Layer

**What you're doing**: Implement the write repository using EF Core context. Keep this sealed for auto-discovery.

```csharp
// In Infra/Features/CustomerProfiles/Repos/CustomerProfileRepository.cs
namespace SlimBus.Infra.Features.CustomerProfiles.Repos;

using SlimBus.Domains.Features.CustomerProfiles.Entities;
using SlimBus.AppServices.Features.CustomerProfiles.Repositories;
using SlimBus.Infra.Contexts;

public sealed class CustomerProfileRepository : ICustomerProfileRepository
{
    private readonly ApplicationDbContext _context;

    public CustomerProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CustomerProfile profile)
        => _context.CustomerProfiles.Add(profile);

    public async Task UpdateAsync(CustomerProfile profile)
        => _context.CustomerProfiles.Update(profile);

    public async Task DeleteAsync(Guid id)
    {
        var profile = await _context.CustomerProfiles.FindAsync(id);
        if (profile != null)
            _context.CustomerProfiles.Remove(profile);
    }

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
```

### Step 5: Use Specs in Handlers/Services

**What you're doing**: Events represent "things that happened" in the domain. Published for cross-aggregate communication and audit trails.

```csharp
// In AppServices/Features/CustomerProfiles/Events/CustomerProfileCreatedEvent.cs
namespace SlimBus.AppServices.Features.CustomerProfiles.Events;

public sealed record CustomerProfileCreatedEvent(
    Guid ProfileId,
    Guid UserId,
    string Email,
    DateTime CreatedAt) : IDomainEvent;

public sealed record CustomerProfileUpdatedEvent(
    Guid ProfileId,
    DateTime UpdatedAt) : IDomainEvent;
```

### Step 5: Create Domain Events

**What you're doing**: Events represent "things that happened" in the domain. Published for cross-aggregate communication and audit trails.

```csharp
// In AppServices/Features/CustomerProfiles/Events/CustomerProfileEvents.cs
namespace SlimBus.AppServices.Features.CustomerProfiles.Events;

public sealed record CustomerProfileCreatedEvent(
    Guid ProfileId,
    Guid UserId,
    string Email,
    DateTime CreatedAt) : IDomainEvent;

public sealed record CustomerProfileUpdatedEvent(
    Guid ProfileId,
    DateTime UpdatedAt) : IDomainEvent;

public sealed record CustomerProfileDeletedEvent(
    Guid ProfileId,
    DateTime DeletedAt) : IDomainEvent;
```

### Step 6: Create Service with Spec Usage

**What you're doing**: For endpoints that need additional orchestration beyond simple CRUD, create handlers that execute business logic: validate, call repository, publish events.

```csharp
// In AppServices/Features/CustomerProfiles/Services/ProfileService.cs
namespace SlimBus.AppServices.Features.CustomerProfiles.Services;

using SlimBus.AppServices.Features.CustomerProfiles.V1;
using SlimBus.AppServices.Features.CustomerProfiles.Events;
using SlimBus.AppServices.Features.CustomerProfiles.Repositories;
using SlimBus.Domains.Features.CustomerProfiles.Entities;
using SlimBus.Infra.Services;

public sealed class ProfileService
{
    private readonly ICustomerProfileRepository _repository;
    private readonly EventPublisher _eventPublisher;

    public ProfileService(
        ICustomerProfileRepository repository,
        EventPublisher eventPublisher)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    public async Task<Guid> CreateAsync(
        CreateCustomerProfileRequest request,
        Guid userId)
    {
        // Step 1: Check for duplicates
        var existing = await _repository.GetByEmailAsync(request.Email);
        if (existing != null)
            throw new InvalidOperationException($"Email {request.Email} already exists");

        // Step 2: Create entity
        var profile = CustomerProfile.Create(
            userId,
            request.FullName,
            request.Email,
            request.DateOfBirth);

        // Step 3: Add to repository
        await _repository.AddAsync(profile);
        await _repository.SaveChangesAsync();

        // Step 4: Publish domain event
        await _eventPublisher.Publish(
            new CustomerProfileCreatedEvent(
                profile.Id,
                profile.UserId,
                profile.Email,
                profile.CreatedAt));

        return profile.Id;
    }
}
```

### Step 6: Create Validation Specs (Optional - if using Specs pattern)

**What you're doing**: Validation specs let you describe complex query business rules declaratively.

```csharp
// In AppServices/Specs/SpecGetCustomerProfileByEmail.cs
namespace SlimBus.AppServices.Specs;

using SlimBus.Domains.Features.CustomerProfiles.Entities;
using Ardalis.Specification;

public sealed class SpecGetCustomerProfileByEmail : Specification<CustomerProfile>
{
    public SpecGetCustomerProfileByEmail(string email)
    {
        Query.Where(x => x.Email == email);
    }
}
```

### Step 7: Register Services in DI Container

**What you're doing**: Wire services in the dependency injection setup.

Done in `InfraSetup.cs` (Infra layer):
```csharp
services.AddScoped<IRepository<CustomerProfile>, EfRepository<CustomerProfile>>();
services.AddScoped<ICustomerProfileRepository, CustomerProfileRepository>();
```

Done in `AppSetup.cs` (AppServices layer):
```csharp
services.AddScoped<ProfileService>();
services.AddValidatorsFromAssembly(typeof(AppSetup).Assembly);
```

### Step 8: Verify Specs Auto-Discovered

**What you're doing**: Specs live in `AppServices/Specs/` and are referenced by name in handlers/endpoints. They are NOT automatically discovered - you call them explicitly:

```csharp
var spec = new SpecGetCustomerProfileById(id);
var profile = await _repository.FirstOrDefaultAsync(spec);
```

---

## Success Validation: Checklist

See [checklist.md](./checklist.md) for full validation gates. Key items:
- [ ] DTOs created with `[GenerateDto]` for responses, manual records for requests
- [ ] Request validators registered via FluentValidation
- [ ] Repository interface defined in AppServices
- [ ] Repository implementation sealed, in Infra Repos/ folder
- [ ] Business logic in services/handlers where needed
- [ ] Domain events published after mutations
- [ ] All code compiles with zero warnings
- [ ] Unit tests for business logic pass

---

## Common Errors & How to Fix Them

### Error: "Repository not registered in DI"

**Why**: Interface/implementation not registered.

**Fix**: Add in Infra setup: `services.AddScoped<ICustomerProfileRepository, CustomerProfileRepository>();`

---

## Complete Working Example

See [examples/customer-profile-crud/](./examples/customer-profile-crud/) for full CRUD workflow applied to CustomerProfile.

---

## Next Steps

Once CRUD operations are complete:

1. **[API Endpoints Skill](../api-endpoints/skill.md)** — Expose via REST endpoints with DTOs

---

**Skill Version**: 1.0.0 | **Status**: Published | **Last Updated**: 2026-03-17
