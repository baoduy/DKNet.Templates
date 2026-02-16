# Validation and Mapping Patterns

## Overview
Validation ensures data integrity, while mapping transforms data between layers. DKNet integrates FluentValidation and Mapster for robust validation and mapping.

## Validation Patterns

### 1. Data Annotations
Use for simple, declarative validation on commands/queries:
```csharp
public sealed record CreateProfileCommand : BaseCommand, IWitResponse<ProfileResult>
{
    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = null!;

    [Phone]
    [StringLength(50)]
    public string Phone { get; set; } = null!;

    [Range(0, 150)]
    public int? Age { get; set; }

    [Url]
    public string? Website { get; set; }
}
```

**Common Attributes**:
- `[Required]` - Field cannot be null/empty
- `[StringLength(max, MinimumLength = min)]` - String length constraints
- `[Range(min, max)]` - Numeric range
- `[EmailAddress]` - Valid email format
- `[Phone]` - Valid phone format
- `[Url]` - Valid URL format
- `[RegularExpression(pattern)]` - Custom regex validation
- `[Compare("PropertyName")]` - Compare with another property
- `[CreditCard]` - Valid credit card number

### 2. FluentValidation
For complex business rules and cross-field validation:
```csharp
internal sealed class CreateProfileCommandValidator : AbstractValidator<CreateProfileCommand>
{
    #region Constructors

    public CreateProfileCommandValidator()
    {
        this.RuleFor(a => a.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .Length(1, 1000).WithMessage("Email must be between 1 and 1000 characters");

        this.RuleFor(a => a.Phone)
            .NotEmpty()
            .Length(6, 50)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format");

        this.RuleFor(a => a.Name)
            .NotEmpty()
            .Length(6, 100)
            .Must(BeValidName).WithMessage("Name contains invalid characters");

        this.RuleFor(a => a.BirthDay)
            .LessThan(DateTime.UtcNow).WithMessage("Birth date must be in the past")
            .GreaterThan(DateTime.UtcNow.AddYears(-150)).WithMessage("Invalid birth date");

        // Conditional validation
        this.When(a => a.Age.HasValue, () =>
        {
            this.RuleFor(a => a.Age!.Value)
                .InclusiveBetween(0, 150);
        });

        // Cross-field validation
        this.RuleFor(a => a)
            .Must(HaveValidAgeAndBirthDay)
            .WithMessage("Age and birth date must match");
    }

    #endregion

    #region Methods

    private static bool BeValidName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && 
               name.All(c => char.IsLetter(c) || char.IsWhiteSpace(c));
    }

    private static bool HaveValidAgeAndBirthDay(CreateProfileCommand command)
    {
        if (!command.Age.HasValue || !command.BirthDay.HasValue)
            return true;

        var calculatedAge = (DateTime.UtcNow - command.BirthDay.Value).TotalDays / 365.25;
        return Math.Abs(calculatedAge - command.Age.Value) < 1;
    }

    #endregion
}
```

### FluentValidation Rules

#### Basic Rules
```csharp
RuleFor(x => x.Name).NotEmpty();
RuleFor(x => x.Name).NotNull();
RuleFor(x => x.Name).Length(min, max);
RuleFor(x => x.Age).InclusiveBetween(0, 150);
RuleFor(x => x.Age).ExclusiveBetween(0, 150);
RuleFor(x => x.Email).EmailAddress();
```

#### String Rules
```csharp
RuleFor(x => x.Name)
    .MinimumLength(2)
    .MaximumLength(100)
    .Matches(@"^[a-zA-Z\s]+$")
    .Must(name => !name.Contains("bad"));
```

#### Comparison Rules
```csharp
RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate);
RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
RuleFor(x => x.ConfirmPassword).Equal(x => x.Password);
```

#### Collection Rules
```csharp
RuleFor(x => x.Addresses)
    .NotEmpty()
    .Must(list => list.Count <= 5).WithMessage("Maximum 5 addresses allowed");

RuleForEach(x => x.Addresses)
    .SetValidator(new AddressValidator());
```

#### Conditional Rules
```csharp
When(x => x.IsCompany, () =>
{
    RuleFor(x => x.CompanyName).NotEmpty();
    RuleFor(x => x.TaxId).NotEmpty();
});

Unless(x => x.IsCompany, () =>
{
    RuleFor(x => x.FirstName).NotEmpty();
    RuleFor(x => x.LastName).NotEmpty();
});
```

#### Async Rules
```csharp
RuleFor(x => x.Email)
    .MustAsync(async (email, cancellation) =>
    {
        return await _emailService.IsUniqueAsync(email);
    })
    .WithMessage("Email already exists");
```

### 3. Handler Validation
For database-dependent validation:
```csharp
public async Task<IResult<ProfileResult>> OnHandle(
    CreateProfileCommand request,
    CancellationToken cancellationToken)
{
    // Check duplicate in database
    if (await repository.IsEmailExistAsync(request.Email))
    {
        return Result.Fail<ProfileResult>($"Email {request.Email} already exists.");
    }

    // Check related entity exists
    var company = await companyRepo.GetByIdAsync(request.CompanyId, cancellationToken);
    if (company == null)
    {
        return Result.Fail<ProfileResult>("Company not found.");
    }

    // Proceed with business logic
    // ...
}
```

## Mapping Patterns

### 1. Automatic Mapping with Attributes

#### Command to Entity
```csharp
[MapsTo(typeof(CustomerProfile))]
public sealed record CreateProfileCommand : BaseCommand, IWitResponse<ProfileResult>
{
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Phone { get; set; } = null!;
}

// Usage in handler
var profile = mapper.Map<CustomerProfile>(request);
```

#### Entity to DTO
```csharp
[MapsFrom(typeof(CustomerProfile))]
public sealed record ProfileResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string MembershipNo { get; init; } = null!;
}

// Usage in query handler
return await repo.Query<ProfileResult>(p => p.Id == request.Id)
    .FirstOrDefaultAsync(cancellationToken);
```

### 2. Custom Mapping Configuration
For complex mappings, create custom configurations:
```csharp
public class ProfileMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Command to Entity
        config.NewConfig<CreateProfileCommand, CustomerProfile>()
            .Map(dest => dest.Email, src => src.Email.ToLower())
            .Map(dest => dest.Name, src => src.Name.Trim())
            .Map(dest => dest, src => src.UserId)  // Map UserId to CreatedBy
            .ConstructUsing(src => new CustomerProfile(
                src.Name,
                src.MembershipNo,
                src.Email,
                src.Phone,
                src.UserId));

        // Entity to DTO
        config.NewConfig<CustomerProfile, ProfileResult>()
            .Map(dest => dest.DisplayName, src => $"{src.Name} ({src.MembershipNo})")
            .Map(dest => dest.IsNew, src => src.CreatedDate > DateTime.UtcNow.AddDays(-30));

        // Update Command to Entity
        config.NewConfig<UpdateProfileCommand, CustomerProfile>()
            .IgnoreNullValues(true)  // Only map non-null values
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Phone, src => src.Phone);
    }
}
```

### 3. Mapping in Handlers

#### Create Handler with Mapping
```csharp
internal sealed class CreateProfileCommandHandler(
    ICustomerProfileRepo repository,
    IMapper mapper)
    : IHandler<CreateProfileCommand, ProfileResult>
{
    public async Task<IResult<ProfileResult>> OnHandle(
        CreateProfileCommand request,
        CancellationToken cancellationToken)
    {
        // Map command to entity
        var profile = mapper.Map<CustomerProfile>(request);

        // Save entity
        await repository.AddAsync(profile, cancellationToken);

        // Return lazy-mapped result (deferred until SaveChanges)
        return mapper.ResultOf<ProfileResult>(profile);
    }
}
```

#### Update Handler with Mapping
```csharp
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
            return Result.Fail<ProfileResult>("Profile not found");

        // Adapt command properties to entity (ignoring nulls)
        mapper.Map(request, profile);

        await repository.UpdateAsync(profile, cancellationToken);

        return mapper.ResultOf<ProfileResult>(profile);
    }
}
```

#### Query Handler with Projection
```csharp
internal sealed class SingleProfileQueryHandler(
    IReadRepository<CustomerProfile> repo)
    : IHandler<ProfileQuery, ProfileResult>
{
    public async Task<ProfileResult?> OnHandle(
        ProfileQuery request,
        CancellationToken cancellationToken)
    {
        // Automatic projection using MapsFrom attribute
        return await repo.Query<ProfileResult>(p => p.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

### 4. Lazy Mapping
For entities where ID is generated by the database:
```csharp
// Add entity
await repository.AddAsync(profile, cancellationToken);

// Create lazy mapping result
return mapper.ResultOf<ProfileResult>(profile);

// After SaveChanges is called, the mapping executes with the generated ID
```

### 5. Collection Mapping
```csharp
// Map collection of entities to DTOs
var profileDtos = mapper.Map<List<ProfileResult>>(profiles);

// Map collection with projection
var results = await repo.GetAll()
    .ProjectToType<ProfileResult>()
    .ToListAsync(cancellationToken);
```

## Validation Flow

1. **Request Pipeline**:
   - Data Annotations validation (automatic)
   - FluentValidation (automatic if validator exists)
   - Custom handler validation (manual)

2. **Automatic Validation**:
```csharp
// DKNet automatically validates before handler execution
// If validation fails, returns 400 Bad Request with validation errors
```

3. **Manual Validation**:
```csharp
public async Task<IResult<ProfileResult>> OnHandle(
    CreateProfileCommand request,
    CancellationToken cancellationToken)
{
    // Custom validation in handler
    if (await repository.IsEmailExistAsync(request.Email))
    {
        return Result.Fail<ProfileResult>(
            new ValidationError(nameof(request.Email), "Email already exists"));
    }

    // Proceed with business logic
    // ...
}
```

## Best Practices

### Validation
1. **Layer Validation Appropriately**:
   - Data Annotations: Format/type validation
   - FluentValidation: Business rules
   - Handler: Database-dependent validation

2. **Clear Error Messages**: Provide user-friendly messages
3. **Fail Fast**: Validate early to avoid unnecessary processing
4. **Centralize Rules**: Reuse validators where possible
5. **Test Validators**: Write unit tests for validation logic
6. **Async Validation**: Use `MustAsync` for database checks only when necessary
7. **Conditional Validation**: Use `When` for scenario-specific rules

### Mapping
1. **Use Attributes**: `[MapsTo]` and `[MapsFrom]` for simple mappings
2. **Custom Config**: Create `IRegister` for complex mappings
3. **Lazy Mapping**: Use `ResultOf<T>()` when entity ID isn't set yet
4. **Projection**: Use `Query<TResult>()` for optimized database queries
5. **Null Handling**: Use `IgnoreNullValues()` for updates
6. **Naming Conventions**: Match property names for automatic mapping
7. **Test Mappings**: Verify mappings work correctly
8. **Performance**: Prefer projection over loading full entities

## File Organization
```
Validators:
{ProjectName}.AppServices/{FeatureName}/V{Version}/Actions/{Command}Validator.cs

Mapping Configs:
{ProjectName}.AppServices/{FeatureName}/Mappings/{Feature}MappingConfig.cs
```

## Common Validation Scenarios

### Email Uniqueness
```csharp
RuleFor(x => x.Email)
    .MustAsync(async (email, cancellation) =>
    {
        return !await _repository.IsEmailExistAsync(email);
    })
    .WithMessage("Email already exists");
```

### Complex Password
```csharp
RuleFor(x => x.Password)
    .NotEmpty()
    .MinimumLength(8)
    .Matches(@"[A-Z]").WithMessage("Password must contain uppercase letter")
    .Matches(@"[a-z]").WithMessage("Password must contain lowercase letter")
    .Matches(@"[0-9]").WithMessage("Password must contain digit")
    .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain special character");
```

### Date Range
```csharp
RuleFor(x => x.StartDate)
    .NotEmpty()
    .LessThan(x => x.EndDate)
    .WithMessage("Start date must be before end date");

RuleFor(x => x.EndDate)
    .NotEmpty()
    .GreaterThan(DateTime.UtcNow)
    .WithMessage("End date must be in the future");
```

### Nested Objects
```csharp
RuleFor(x => x.Address)
    .NotNull()
    .SetValidator(new AddressValidator());
```
