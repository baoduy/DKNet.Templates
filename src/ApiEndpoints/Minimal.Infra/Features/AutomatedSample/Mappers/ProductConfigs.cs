using Minimal.Domains.Features.AutomatedSample.Entities;

namespace Minimal.Infra.Features.AutomatedSample.Mappers;

internal sealed class ProductConfigs : DefaultEntityTypeConfiguration<Product>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(p => p.Name).IsUnique();
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.ToTable("Products", "sample");
    }

    #endregion
}
