using DKNet.EfCore.Specifications;

namespace SlimBus.AppServices.Profiles.V1.Specs;

internal sealed class SpecGetCustomerProfile : Specification<CustomerProfile>
{
    public SpecGetCustomerProfile(Guid? byId = null, string? byEmail = null)
    {
        var predicator = CreatePredicate();
        if (byId is not null)
        {
            predicator = predicator.And(a => a.Id == byId);
        }

        if (!string.IsNullOrEmpty(byEmail))
        {
            predicator = predicator.And(a => a.Email == byEmail);
        }

        WithFilter(predicator);
    }
}