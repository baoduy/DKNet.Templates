using Minimal.Domains.Features.LoyaltyMemberships.Entities;

namespace Minimal.Infra.Features.LoyaltyMemberships.Mappers;

internal sealed class LoyaltyMembershipConfigs : DefaultEntityTypeConfiguration<LoyaltyMembership>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<LoyaltyMembership> builder)
    {
        base.Configure(builder);

        builder.HasIndex(m => m.MemberName).IsUnique();
        builder.Property(m => m.MemberName).HasMaxLength(150).IsRequired();
        builder.Property(m => m.Tier).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.ToTable("LoyaltyMemberships", DomainSchemas.Profile);
    }

    #endregion
}
