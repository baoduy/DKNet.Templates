using DKNet.EfCore.DtoGenerator;
using Minimal.AppServices.Extensions;

namespace Minimal.AppServices.CustomerProfiles.V1;

[GenerateDto(typeof(CustomerProfile),Exclude = [])]
[MapsFrom(typeof(CustomerProfile))]
public sealed partial record CustomerProfileDto;