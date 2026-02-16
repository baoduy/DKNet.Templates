# EfCore Configuration Development

## Overview
EF Core configurations define how your domain entities map to database tables. Using DKNet, configurations are automatically discovered and applied.

## Configuration Base Class

### DefaultEntityTypeConfiguration<TEntity>
All entity configurations should inherit from `DefaultEntityTypeConfiguration<TEntity>`:
```csharp
using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.Infra.Features.Profiles.Mappers;

internal sealed class ProfileMapper : DefaultEntityTypeConfiguration<CustomerProfile>
{
    public override void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        base.Configure(builder);
        
        // Your custom configuration
    }
}
```

**Why use DefaultEntityTypeConfiguration?**
- Provides default configurations for audit fields (CreatedBy, UpdatedBy, etc.)
- Handles common patterns (RowVersion for concurrency, IsActive filtering)
- Ensures consistency across all entities

## Configuration Pattern

### 1. Indexes
Define indexes for frequently queried or unique columns:
```csharp
builder.HasIndex(p => p.Email).IsUnique();
builder.HasIndex(p => p.MembershipNo).IsUnique();
builder.HasIndex(p => p.CreatedDate); // For audit queries
```

### 2. Property Constraints
Define column constraints:
```csharp
// Required fields
builder.Property(p => p.Email).HasMaxLength(150).IsRequired();
builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
builder.Property(p => p.MembershipNo).HasMaxLength(50).IsRequired();

// Optional fields
builder.Property(p => p.Avatar).HasMaxLength(50);
builder.Property(p => p.Phone).HasMaxLength(50).IsRequired(false);

// Special types
builder.Property(p => p.BirthDay).HasColumnType("Date");
```

### 3. Table Mapping
Specify table name and schema:
```csharp
builder.ToTable("CustomerProfiles", DomainSchemas.Profile);
```

**Note**: This should match the `[Table]` attribute on the entity, but the mapper configuration takes precedence.

### 4. Relationships
Configure entity relationships:
```csharp
// One-to-Many
builder.HasMany(p => p.Addresses)
    .WithOne()
    .HasForeignKey(a => a.ProfileId)
    .OnDelete(DeleteBehavior.Cascade);

// One-to-One
builder.HasOne(p => p.Detail)
    .WithOne()
    .HasForeignKey<ProfileDetail>(d => d.ProfileId);

// Many-to-Many
builder.HasMany(p => p.Tags)
    .WithMany(t => t.Profiles)
    .UsingEntity(j => j.ToTable("ProfileTags"));
```

### 5. Value Conversions
For complex types or enums:
```csharp
// Enum to string
builder.Property(p => p.Status)
    .HasConversion<string>()
    .HasMaxLength(50);

// JSON serialization
builder.Property(p => p.Metadata)
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
        v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null))
    .HasColumnType("nvarchar(max)");
```

## Complete Configuration Example

```csharp
using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.Infra.Features.Profiles.Mappers;

internal sealed class ProfileMapper : DefaultEntityTypeConfiguration<CustomerProfile>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        // IMPORTANT: Always call base.Configure first
        base.Configure(builder);

        // Indexes
        builder.HasIndex(p => p.Email).IsUnique();
        builder.HasIndex(p => p.MembershipNo).IsUnique();

        // Property configurations
        builder.Property(p => p.Avatar).HasMaxLength(50);
        builder.Property(p => p.BirthDay).HasColumnType("Date");
        builder.Property(p => p.Email).HasMaxLength(150).IsRequired();
        builder.Property(p => p.MembershipNo).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(50).IsRequired(false);

        // Table mapping
        builder.ToTable("CustomerProfiles", DomainSchemas.Profile);
    }

    #endregion
}
```

## DbContext Integration

### Using Auto-Configuration
DKNet automatically discovers and applies all configurations:

```csharp
public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Auto-discover all entity configurations
        modelBuilder.UseAutoConfigModel(
            typeof(ProfileMapper).Assembly,
            typeof(CustomerProfile).Assembly);
    }
}
```

### Service Registration
Register DbContext with auto-configuration in Program.cs:
```csharp
services.AddDbContextWithHook<CoreDbContext>(
    (sp, builder) =>
    {
        builder.UseSqlServer(connectionString);
    })
    .UseAutoConfigModel([
        typeof(ProfileMapper).Assembly,
        typeof(CustomerProfile).Assembly
    ])
    .UseAutoDataSeeding([typeof(ProfileData).Assembly]);
```

## Data Seeding

### Static Data Class
Create seed data for initial/test data:
```csharp
namespace SlimBus.Infra.Features.Profiles.StaticData;

internal sealed class ProfileData : IStaticData
{
    #region Properties

    public int Order => 1;

    #endregion

    #region Methods

    public void Seed(ModelBuilder modelBuilder)
    {
        var profiles = new[]
        {
            new CustomerProfile(
                Guid.NewGuid(),
                "John Doe",
                "MEM001",
                "john.doe@example.com",
                "1234567890",
                "system"),
            new CustomerProfile(
                Guid.NewGuid(),
                "Jane Smith",
                "MEM002",
                "jane.smith@example.com",
                "0987654321",
                "system")
        };

        modelBuilder.Entity<CustomerProfile>().HasData(profiles);
    }

    #endregion
}
```

## Best Practices

1. **Always call base.Configure()** first to inherit default configurations
2. **Use sealed classes** for mapper configurations
3. **Mark as internal** - configurations are infrastructure details
4. **Group by feature** - keep mappers in feature-specific folders
5. **Consistent naming** - Use `{EntityName}Mapper` convention
6. **Index strategy**:
   - Unique constraints on business keys (Email, MembershipNo)
   - Indexes on foreign keys (automatically created)
   - Indexes on frequently filtered columns
7. **String lengths**: Always specify `HasMaxLength()` for string properties
8. **Required vs Optional**: Explicitly specify `.IsRequired()` or `.IsRequired(false)`
9. **Schema organization**: Use domain schemas to group related tables
10. **Relationships**: Configure from the principal (parent) entity side

## File Location
Place mapper files in:
```
{ProjectName}.Infra/Features/{FeatureName}/Mappers/{EntityName}Mapper.cs
```

## Migration Commands
```bash
# Add migration
dotnet ef migrations add InitialCreate --project SlimBus.Infra --startup-project SlimBus.Api

# Update database
dotnet ef database update --project SlimBus.Infra --startup-project SlimBus.Api

# Remove last migration
dotnet ef migrations remove --project SlimBus.Infra --startup-project SlimBus.Api
```

## Common Column Types
```csharp
// Date only (no time)
.HasColumnType("Date")

// Decimal with precision
.HasColumnType("decimal(18,2)")

// Large text
.HasColumnType("nvarchar(max)")

// JSON (SQL Server 2016+)
.HasColumnType("nvarchar(max)")  // Use with JSON conversion

// Binary
.HasColumnType("varbinary(max)")
```
