using DKNet.EfCore.Specifications.Definitions;
using Minimal.Domains.Features.AutomatedSample.Entities;

namespace Minimal.AppServices.AutomatedSample.V1.Specs;

internal sealed class SpecProductByName : Specification<Product>
{
    public SpecProductByName(string name)
    {
        WithFilter(CreatePredicate().And(p => p.Name == name));
    }
}
