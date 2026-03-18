// ==================================================
// REQUEST DTO TEMPLATE
// ==================================================
// Location: Minimal.AppServices/Features/<FEATURE>/V1/<OPERATION>Request.cs
// 
// INSTRUCTIONS:
// 1. Replace <FEATURE> with your feature name (e.g., "CustomerProfiles")
// 2. Replace <OPERATION> with Create, Update, Delete (e.g., "CreateCustomerProfileRequest")
// 3. Update properties to match your domain entity fields
// 4. Add this file to your AppServices project
// 5. Create a validator class (see validator-template.cs)

using System;

namespace Minimal.AppServices.Features.<FEATURE>.V1;

public sealed record Create<FEATURE>Request(
    // TODO: Replace with your DTO properties
    // Example:
    // string FullName,
    // string Email,
    // DateTime? DateOfBirth
);

public sealed record Update<FEATURE>Request(
    // TODO: Replace with your DTO properties for updates
    // (Usually same as Create, but sometimes subset)
);
