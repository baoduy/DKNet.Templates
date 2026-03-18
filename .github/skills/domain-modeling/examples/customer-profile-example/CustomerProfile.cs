namespace Minimal.Domains.Features.CustomerProfiles.Entities;

/// <summary>
/// Customer profile domain entity containing customer information.
/// Manages encapsulated state and mutations.
/// 
/// This entity represents a customer's profile with contact information,
/// personal details, and audit timestamps. It enforces business rules
/// through encapsulated methods rather than direct property mutations.
/// </summary>
public sealed class CustomerProfile
{
    /// <summary>
    /// Unique identifier for this customer profile.
    /// Generated as a GUID upon creation.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Associated user ID (foreign key).
    /// Links this customer profile to the user that created/owns it.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Customer's full name.
    /// Required field, max 200 characters (enforced in mapper).
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>
    /// Customer's email address.
    /// Required field, max 256 characters, unique (enforced in mapper).
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Customer's date of birth.
    /// Optional field; nullable DateTime represents "unknown" birth date.
    /// </summary>
    public DateTime? DateOfBirth { get; init; }

    /// <summary>
    /// Timestamp when profile was created (UTC).
    /// Set once upon creation; never modified.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when profile was last updated (UTC).
    /// Updated every time the profile is modified via Update() method.
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Factory method to create a new customer profile.
    /// 
    /// Centralizes instantiation logic and ensures all required properties
    /// are initialized with valid values. This is the ONLY way to create
    /// a new CustomerProfile (enforced by sealed init-only properties).
    /// 
    /// Usage:
    /// var profile = CustomerProfile.Create(
    ///     userId: Guid.Parse("..."),
    ///     fullName: "John Smith",
    ///     email: "john@example.com",
    ///     dateOfBirth: new DateTime(1990, 5, 15));
    /// </summary>
    /// <param name="userId">ID of the user creating this profile</param>
    /// <param name="fullName">Customer's full name</param>
    /// <param name="email">Customer's email address</param>
    /// <param name="dateOfBirth">Customer's date of birth (optional)</param>
    /// <returns>New CustomerProfile instance with generated ID and timestamps</returns>
    public static CustomerProfile Create(
        Guid userId,
        string fullName,
        string email,
        DateTime? dateOfBirth = null)
    {
        return new CustomerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = fullName,
            Email = email,
            DateOfBirth = dateOfBirth,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Update customer profile information.
    /// 
    /// Encapsulated mutation method that ensures business rules are enforced
    /// when profile data changes. This is the ONLY way to modify a profile
    /// (UpdatedAt is auto-set; no direct property mutation allowed).
    /// 
    /// Usage:
    /// profile.Update(
    ///     fullName: "Jane Smith",
    ///     email: "jane@example.com",
    ///     dateOfBirth: new DateTime(1992, 3, 22));
    /// </summary>
    /// <param name="fullName">Updated full name</param>
    /// <param name="email">Updated email address</param>
    /// <param name="dateOfBirth">Updated date of birth (optional)</param>
    public void Update(string fullName, string email, DateTime? dateOfBirth = null)
    {
        FullName = fullName;
        Email = email;
        DateOfBirth = dateOfBirth;
        UpdatedAt = DateTime.UtcNow;  // Always update timestamp on any change
    }
}
