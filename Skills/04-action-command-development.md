# Action/Command Development with Fluent Pattern

## Overview
Actions (Commands) represent write operations that change system state. They follow the CQRS pattern using DKNet's Fluent library.

## Command Structure

### 1. Command Definition
Commands are immutable records that implement `IWitResponse<TResult>`:
```csharp
using Fluents.Requests;

namespace SlimBus.AppServices.Profiles.V1.Actions;

[MapsTo(typeof(CustomerProfile))]
public sealed record CreateProfileCommand : BaseCommand, IWitResponse<ProfileResult>
{
    #region Properties

    [Required]
    public string Email { get; set; } = null!;

    [StringLength(150)]
    [Required]
    public string Name { get; set; } = null!;

    [Phone]
    public string Phone { get; set; } = null!;

    [JsonIgnore]
    [Description("This property is not used in the mapping, it will be set by the membership provider if not provided.")]
    public string MembershipNo { get; set; } = null!;

    #endregion
}
```

**Key Points**:
- Use `record` for immutability
- Mark as `sealed` for performance
- Inherit from `BaseCommand` (provides common audit fields like UserId)
- Implement `IWitResponse<TResult>` from Fluents.Requests
- Use `[MapsTo(typeof(Entity))]` for automatic mapping configuration
- Decorate properties with validation attributes

### 2. Command Validator
Use FluentValidation for complex validation rules:
```csharp
internal sealed class CreateProfileCommandValidator : AbstractValidator<CreateProfileCommand>
{
    #region Constructors

    public CreateProfileCommandValidator()
    {
        this.RuleFor(a => a.Email)
            .NotEmpty()
            .EmailAddress()
            .Length(1, 1000);

        this.RuleFor(a => a.Phone)
            .NotEmpty()
            .Length(6, 50);

        this.RuleFor(a => a.Name)
            .NotEmpty()
            .Length(6, 100);
    }

    #endregion
}
```

**Validation Strategy**:
- Data Annotations (on command) - basic validation
- FluentValidation - complex business rules
- Handler validation - database-dependent rules (e.g., uniqueness)

### 3. Command Handler
Handlers implement `IHandler<TCommand, TResult>`:
```csharp
internal sealed class CreateProfileCommandHandler(
    ICustomerProfileRepo repository,
    IMembershipService membershipProvider,
    IMapper mapper)
    : IHandler<CreateProfileCommand, ProfileResult>
{
    #region Methods

    public async Task<IResult<ProfileResult>> OnHandle(
        CreateProfileCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Business logic/validation
        if (string.IsNullOrWhiteSpace(request.MembershipNo))
        {
            request.MembershipNo = await membershipProvider.NextValueAsync();
        }

        // 2. Check duplicates
        if (await repository.IsEmailExistAsync(request.Email))
        {
            return Result.Fail<ProfileResult>($"Email {request.Email} is already existed.");
        }

        // 3. Map to entity
        var profile = mapper.Map<CustomerProfile>(request);

        if (string.IsNullOrEmpty(profile.MembershipNo))
        {
            throw new NoNullAllowedException(nameof(profile.MembershipNo));
        }

        // 4. Add to repository
        await repository.AddAsync(profile, cancellationToken);

        // 5. Add domain event
        profile.AddEvent(new ProfileCreatedEvent(profile.Id, profile.Name));

        // 6. Return lazy mapping result
        return mapper.ResultOf<ProfileResult>(profile);
    }

    #endregion
}
```

## Command Types

### Create Command
```csharp
public sealed record CreateProfileCommand : BaseCommand, IWitResponse<ProfileResult>
{
    [Required] public string Email { get; set; } = null!;
    [Required] public string Name { get; set; } = null!;
    [Phone] public string Phone { get; set; } = null!;
}

internal sealed class CreateProfileCommandHandler(
    ICustomerProfileRepo repository,
    IMapper mapper)
    : IHandler<CreateProfileCommand, ProfileResult>
{
    public async Task<IResult<ProfileResult>> OnHandle(
        CreateProfileCommand request,
        CancellationToken cancellationToken)
    {
        var entity = mapper.Map<DomainEntity>(request);
        await repository.AddAsync(entity, cancellationToken);
        entity.AddEvent(new EntityCreatedEvent(entity.Id));
        return mapper.ResultOf<ProfileResult>(entity);
    }
}
```

### Update Command
```csharp
public sealed record UpdateProfileCommand : BaseCommand, IWitResponse<ProfileResult>
{
    [FromRoute] public Guid Id { get; set; }
    
    [StringLength(150)] public string? Name { get; set; }
    [Phone] public string? Phone { get; set; }
    public DateTime? BirthDay { get; set; }
    public string? Avatar { get; set; }
}

internal sealed class UpdateProfileCommandHandler(
    ICustomerProfileRepo repository,
    IMapper mapper)
    : IHandler<UpdateProfileCommand, ProfileResult>
{
    public async Task<IResult<ProfileResult>> OnHandle(
        UpdateProfileCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (profile == null)
        {
            return Result.Fail<ProfileResult>("Profile not found");
        }

        profile.Update(request.Avatar, request.Name, request.Phone, request.BirthDay, request.UserId);
        
        await repository.UpdateAsync(profile, cancellationToken);
        
        return mapper.ResultOf<ProfileResult>(profile);
    }
}
```

### Delete Command
```csharp
public sealed record DeleteProfileCommand : BaseCommand, IWitResponse
{
    [FromRoute] public Guid Id { get; set; }
}

internal sealed class DeleteProfileCommandHandler(
    ICustomerProfileRepo repository)
    : IHandler<DeleteProfileCommand>
{
    public async Task<IResult> OnHandle(
        DeleteProfileCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (profile == null)
        {
            return Result.Fail("Profile not found");
        }

        await repository.DeleteAsync(profile, cancellationToken);
        
        return Result.Ok();
    }
}
```

## Result Pattern

### Success Result
```csharp
return mapper.ResultOf<ProfileResult>(profile);  // Lazy mapping
// or
return Result.Ok(result);  // Immediate return
```

### Failure Result
```csharp
return Result.Fail<ProfileResult>("Error message");
// or
return Result.Fail<ProfileResult>(new ValidationError("Field", "Error"));
```

## Mapping Configuration

### Automatic Mapping
Use `[MapsTo]` attribute for simple mappings:
```csharp
[MapsTo(typeof(CustomerProfile))]
public sealed record CreateProfileCommand : BaseCommand, IWitResponse<ProfileResult>
{
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
}
```

### Custom Mapping
For complex scenarios, create Mapster configurations:
```csharp
public class ProfileMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CreateProfileCommand, CustomerProfile>()
            .Map(dest => dest.Email, src => src.Email.ToLower())
            .Map(dest => dest.Name, src => src.Name.Trim());
    }
}
```

## Domain Events

### Event Definition
```csharp
public record ProfileCreatedEvent(Guid ProfileId, string Name);
```

### Adding Events
```csharp
profile.AddEvent(new ProfileCreatedEvent(profile.Id, profile.Name));
```

Events are published automatically after `SaveChangesAsync()`.

## Best Practices

1. **Immutability**: Use `record` types for commands
2. **Sealed Classes**: Mark handlers as `sealed` for performance
3. **Internal Handlers**: Handlers should be internal (implementation details)
4. **Dependency Injection**: Use primary constructors for clean DI
5. **Validation Layers**:
   - Data Annotations: Format/type validation
   - FluentValidation: Business rules
   - Handler: Database-dependent validation
6. **Return Types**:
   - `IResult<TResult>` for operations returning data
   - `IResult` for operations without return data
7. **Error Handling**: Use Result pattern, not exceptions for business errors
8. **Domain Events**: Emit events for important state changes
9. **Lazy Mapping**: Use `mapper.ResultOf<T>()` for entities that may not have IDs yet
10. **CancellationToken**: Always pass through for async operations

## File Organization
```
{ProjectName}.AppServices/
  {FeatureName}/
    V{Version}/
      Actions/
        Create.cs      (Command + Validator + Handler)
        Update.cs
        Delete.cs
```

## Versioning
When creating a new version:
1. Copy the V1 folder to V2
2. Make necessary changes
3. Update endpoint mappings to use new version
4. Keep old version for backward compatibility
