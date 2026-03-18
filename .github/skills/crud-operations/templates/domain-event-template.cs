// ==================================================
// DOMAIN EVENT TEMPLATE
// ==================================================
// Location: SlimBus.AppServices/Features/<FEATURE>/Events/<FEATURE>Events.cs
//
// INSTRUCTIONS:
// 1. Replace <FEATURE> with your feature name
// 2. Create a record for each business event (Created, Updated, Deleted)
// 3. Include all data needed for event subscribers
// 4. Keep record properties simple (no complex types)
// 5. Add this to your AppServices project

using System;

namespace SlimBus.AppServices.Features.<FEATURE>.Events;

public sealed record <FEATURE>CreatedEvent(
    // TODO: Replace with your event properties
    // Example:
    // Guid EntityId,
    // Guid UserId,
    // DateTime CreatedAt
) : IDomainEvent;

public sealed record <FEATURE>UpdatedEvent(
    // TODO: Replace with updated properties
    // Example:
    // Guid EntityId,
    // DateTime UpdatedAt
) : IDomainEvent;

public sealed record <FEATURE>DeletedEvent(
    // TODO: Replace with deleted properties
    // Example:
    // Guid EntityId,
    // DateTime DeletedAt
) : IDomainEvent;
