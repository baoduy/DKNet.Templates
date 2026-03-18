namespace Minimal.Domains.Features.YourFeature.Entities;

/// <summary>
/// TODO: Replace with your entity name and description
/// Example: Customer profile domain entity containing customer information.
/// Manages encapsulated state and mutations.
/// </summary>
public sealed class YourEntityName  // TODO: Rename "YourEntityName"
{
    /// <summary>
    /// Unique identifier for this entity.
    /// </summary>
    public required Guid Id { get; init; }

    // TODO: Add your properties here with summaries
    // Example properties:
    // public required string Name { get; init; }
    // public required string Email { get; init; }
    // public DateTime? DateOfBirth { get; init; }
    // public required DateTime CreatedAt { get; init; }
    // public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Factory method to create a new entity instance.
    /// TODO: Add parameters for required properties
    /// </summary>
    public static YourEntityName Create(
        string name,  // TODO: Change parameters to match your entity
        string email)
    {
        return new YourEntityName
        {
            Id = Guid.NewGuid(),
            // TODO: Map parameters to properties
            // Name = name,
            // Email = email,
            // CreatedAt = DateTime.UtcNow,
            // UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Update entity information.
    /// Mutation method ensures encapsulation of state changes.
    /// TODO: Modify to match your entity's mutation patterns
    /// </summary>
    public void Update(string name, string email)  // TODO: Change parameters as needed
    {
        // TODO: Update properties
        // Name = name;
        // Email = email;
        // UpdatedAt = DateTime.UtcNow;
    }
}
