using Minimal.Domains.Features.YourFeature.Entities;  // TODO: Update namespace
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Minimal.Infra.Features.YourFeature.Mappers;  // TODO: Update namespace to match your feature

/// <summary>
/// EF Core mapper configuration for YourEntityName entity.
/// TODO: Rename this class and update comments
/// 
/// Auto-discovered by Scrutor and applied via UseAutoConfigModel.
/// Must be sealed class for Scrutor to auto-register it.
/// Located in Mappers/ subfolder for automatic discovery.
/// </summary>
public sealed class YourEntityNameMapper : IEntityTypeConfiguration<YourEntityName>  // TODO: Rename "YourEntityName"
{
    public void Configure(EntityTypeBuilder<YourEntityName> builder)  // TODO: Update type
    {
        // Table setup
        builder.ToTable("YourTableName", "dbo");  // TODO: Change table name and schema

        // Primary key
        builder.HasKey(x => x.Id);

        // Properties - TODO: Configure your properties based on these examples
        
        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .IsRequired();

        // Example: String property with max length validation
        // builder.Property(x => x.Name)
        //     .HasMaxLength(200)
        //     .IsRequired();

        // Example: Optional string property
        // builder.Property(x => x.Email)
        //     .HasMaxLength(256)
        //     .IsRequired(false);

        // Example: DateTime properties
        // builder.Property(x => x.CreatedAt)
        //     .IsRequired();

        // builder.Property(x => x.UpdatedAt)
        //     .IsRequired();

        // Indexes for common queries - TODO: Uncomment and adjust for your queries
        // builder.HasIndex(x => x.UserId)
        //     .HasDatabaseName("IX_YourTableName_UserId");

        // builder.HasIndex(x => x.Email)
        //     .HasDatabaseName("IX_YourTableName_Email")
        //     .IsUnique();

        // Example: Composite index for multi-column queries
        // builder.HasIndex(x => new { x.UserId, x.CreatedAt })
        //     .HasDatabaseName("IX_YourTableName_UserId_CreatedAt");
    }
}
