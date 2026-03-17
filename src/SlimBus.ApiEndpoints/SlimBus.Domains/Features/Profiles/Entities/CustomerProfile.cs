using SlimBus.Domains.Share;

namespace SlimBus.Domains.Features.Profiles.Entities;

/// <summary>
/// Represents a customer profile aggregate root, encapsulating identity, contact, and membership information.
/// </summary>
public class CustomerProfile : AggregateRoot
{
    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="CustomerProfile"/> with a system-assigned identity.
    /// </summary>
    /// <param name="name">The customer's full name.</param>
    /// <param name="membershipNo">The unique membership number assigned to the customer.</param>
    /// <param name="email">The customer's email address.</param>
    /// <param name="phone">The customer's phone number.</param>
    /// <param name="byUser">The identifier of the user creating the profile.</param>
    public CustomerProfile(
        string name,
        string membershipNo,
        string email,
        string phone,
        string byUser)
        : this(Guid.Empty, name, membershipNo, email, phone, byUser)
    {
        Name = name;
        Email = email;
        MembershipNo = membershipNo;
    }

    /// <summary>
    /// Initializes an existing <see cref="CustomerProfile"/> rehydrated from persistence.
    /// </summary>
    /// <param name="id">The existing aggregate identifier.</param>
    /// <param name="name">The customer's full name.</param>
    /// <param name="membershipNo">The unique membership number assigned to the customer.</param>
    /// <param name="email">The customer's email address.</param>
    /// <param name="phone">The customer's phone number.</param>
    /// <param name="createdBy">The identifier of the user who originally created the profile.</param>
    internal CustomerProfile(
        Guid id,
        string name,
        string membershipNo,
        string email,
        string phone,
        string createdBy)
        : base(id, createdBy)
    {
        Name = name;
        Email = email;
        MembershipNo = membershipNo;

        Update(null, name, phone, null, createdBy);
    }

    #endregion

    #region Properties

    /// <summary>Gets the customer's date of birth, if provided.</summary>
    public DateTime? BirthDay { get; private set; }

    /// <summary>Gets the customer's email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the unique membership number assigned to the customer.</summary>
    public string MembershipNo { get; private set; }

    /// <summary>Gets the customer's full name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the URL or path of the customer's avatar image, if set.</summary>
    public string? Avatar { get; private set; }

    /// <summary>Gets the customer's phone number, if provided.</summary>
    public string? Phone { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Updates the mutable fields of the customer profile.
    /// Non-null and non-empty values for <paramref name="name"/> and <paramref name="phoneNumber"/>
    /// replace the existing values; <see langword="null"/> leaves them unchanged.
    /// </summary>
    /// <param name="avatar">The new avatar URL or path, or <see langword="null"/> to clear it.</param>
    /// <param name="name">The new full name, or <see langword="null"/>/<see cref="string.Empty"/> to keep the current value.</param>
    /// <param name="phoneNumber">The new phone number, or <see langword="null"/>/<see cref="string.Empty"/> to keep the current value.</param>
    /// <param name="birthday">The new date of birth, or <see langword="null"/> to clear it.</param>
    /// <param name="userId">The identifier of the user performing the update.</param>
    public void Update(string? avatar, string? name, string? phoneNumber, DateTime? birthday, string userId)
    {
        Avatar = avatar;
        BirthDay = birthday;

        if (!string.IsNullOrEmpty(name))
        {
            Name = name;
        }

        if (!string.IsNullOrEmpty(phoneNumber))
        {
            Phone = phoneNumber;
        }

        SetUpdatedBy(userId);
    }

    #endregion
}