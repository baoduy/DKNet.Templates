# EfCore Domain Entity Development

## Overview
Domain entities are the heart of your business logic. They should be rich, encapsulated, and enforce business rules.

## Entity Base Classes (DKNet.EfCore.Abstractions)

### AggregateRoot
Use `AggregateRoot` for entities that are the root of an aggregate:
```csharp
public class CustomerProfile : AggregateRoot
{
    // Entity implementation
}
```

`AggregateRoot` inherits from `AuditedEntity<Guid>` which provides:
- `Id` (Guid)
- `CreatedBy`, `CreatedDate`
- `UpdatedBy`, `UpdatedDate`
- `IsActive`, `RowVersion`
- Domain event support via `AddEvent()`, `RemoveEvent()`, `GetEvents()`

## Entity Structure Pattern

### 1. Table Attribute
Always specify table name and schema:
```csharp
[Table("CustomerProfiles", Schema = DomainSchemas.Profile)]
public class CustomerProfile : AggregateRoot
{
    // ...
}
```

### 2. Constructors
Provide two constructors:

**Public Constructor** - For creating new entities:
```csharp
public CustomerProfile(
    string name,
    string membershipNo,
    string email,
    string phone,
    string byUser)
    : this(Guid.Empty, name, membershipNo, email, phone, byUser)
{
    this.Name = name;
    this.Email = email;
    this.MembershipNo = membershipNo;
}
```

**Internal Constructor** - For EF Core rehydration:
```csharp
internal CustomerProfile(
    Guid id,
    string name,
    string membershipNo,
    string email,
    string phone,
    string createdBy)
    : base(id, createdBy)
{
    this.Name = name;
    this.Email = email;
    this.MembershipNo = membershipNo;
    this.Update(null, name, phone, null, createdBy);
}
```

### 3. Properties
Use **private setters** to enforce encapsulation:
```csharp
public string Name { get; private set; }
public string Email { get; private set; }
public string MembershipNo { get; private set; }
public string? Avatar { get; private set; }
public string? Phone { get; private set; }
public DateTime? BirthDay { get; private set; }
```

### 4. Methods
Provide public methods for state changes:
```csharp
public void Update(string? avatar, string? name, string? phoneNumber, DateTime? birthday, string userId)
{
    this.Avatar = avatar;
    this.BirthDay = birthday;

    if (!string.IsNullOrEmpty(name))
    {
        this.Name = name;
    }

    if (!string.IsNullOrEmpty(phoneNumber))
    {
        this.Phone = phoneNumber;
    }

    this.SetUpdatedBy(userId);
}
```

## Domain Events

### Adding Events
Domain events signal important state changes:
```csharp
public void Create()
{
    // Business logic
    this.AddEvent(new ProfileCreatedEvent(this.Id, this.Name));
}
```

### Event Definition
Events should be immutable records:
```csharp
public record ProfileCreatedEvent(Guid ProfileId, string Name);
```

## Repository Interface

Define repository interfaces in the Domains layer:
```csharp
namespace SlimBus.Domains.Features.Profiles.Repos;

public interface ICustomerProfileRepo : IWriteRepository<CustomerProfile>
{
    Task<bool> IsEmailExistAsync(string email);
    Task<CustomerProfile?> GetByEmailAsync(string email);
}
```

Interface inheritance:
- `IReadRepository<TEntity>` - For read-only operations
- `IWriteRepository<TEntity>` - Includes read + write operations

## Best Practices

1. **Encapsulation**: Never expose setters publicly
2. **Validation**: Perform validation in methods, not constructors
3. **Invariants**: Maintain entity invariants through all state changes
4. **Immutability**: Make value objects and domain events immutable
5. **No Anemic Models**: Entities should contain business logic, not just data
6. **Constructor Parameters**: Required fields go in constructor, optional in Update methods
7. **Audit Fields**: Use `SetUpdatedBy(userId)` to track changes
8. **Guid Keys**: Use `Guid.Empty` for new entities, let the database generate the ID
9. **Schema Organization**: Group related tables using schemas (e.g., `DomainSchemas.Profile`)
10. **Nullable Properties**: Use `?` for optional properties (`string?`, `DateTime?`)

## File Location
Place entity files in:
```
{ProjectName}.Domains/Features/{FeatureName}/Entities/{EntityName}.cs
```

## Complete Example
```csharp
using System.ComponentModel.DataAnnotations.Schema;
using SlimBus.Domains.Share;

namespace SlimBus.Domains.Features.Profiles.Entities;

[Table("CustomerProfiles", Schema = DomainSchemas.Profile)]
public class CustomerProfile : AggregateRoot
{
    #region Constructors

    public CustomerProfile(
        string name,
        string membershipNo,
        string email,
        string phone,
        string byUser)
        : this(Guid.Empty, name, membershipNo, email, phone, byUser)
    {
        this.Name = name;
        this.Email = email;
        this.MembershipNo = membershipNo;
    }

    internal CustomerProfile(
        Guid id,
        string name,
        string membershipNo,
        string email,
        string phone,
        string createdBy)
        : base(id, createdBy)
    {
        this.Name = name;
        this.Email = email;
        this.MembershipNo = membershipNo;
        this.Update(null, name, phone, null, createdBy);
    }

    #endregion

    #region Properties

    public DateTime? BirthDay { get; private set; }
    public string Email { get; private set; }
    public string MembershipNo { get; private set; }
    public string Name { get; private set; }
    public string? Avatar { get; private set; }
    public string? Phone { get; private set; }

    #endregion

    #region Methods

    public void Update(string? avatar, string? name, string? phoneNumber, DateTime? birthday, string userId)
    {
        this.Avatar = avatar;
        this.BirthDay = birthday;

        if (!string.IsNullOrEmpty(name))
        {
            this.Name = name;
        }

        if (!string.IsNullOrEmpty(phoneNumber))
        {
            this.Phone = phoneNumber;
        }

        this.SetUpdatedBy(userId);
    }

    #endregion
}
```
