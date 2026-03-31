using DKNet.EfCore.DtoGenerator;

namespace Minimal.AppServices.CustomerProfiles.V1;

[GenerateDto(typeof(CustomerProfile), Exclude = [])]
[MapsFrom(typeof(CustomerProfile))]
public sealed partial record CustomerProfileDto;