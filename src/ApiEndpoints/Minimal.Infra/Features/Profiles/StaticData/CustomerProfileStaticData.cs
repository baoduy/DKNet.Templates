using Minimal.Domains.Features.Profiles.Entities;

namespace Minimal.Infra.Features.Profiles.StaticData;

internal sealed class CustomerProfileStaticData : DataSeedingConfiguration<CustomerProfile>
{
    protected override ValueTask<ICollection<CustomerProfile>> GetDataAsync(CancellationToken cancellation = new CancellationToken())
    {
        return ValueTask.FromResult<ICollection<CustomerProfile>>(
        [
            new(
                new Guid("A6B50327-160E-423C-9C0B-C125588E6025"),
                "Steven Hoang",
                "MS12345",
                "abc@gmail.com",
                "123456789",
                SharedConsts.SystemAccount)
        ]);
    }
}