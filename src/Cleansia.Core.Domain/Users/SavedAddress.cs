using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Users;

/// <summary>
/// A saved address belonging to a user. Wraps a shared <see cref="Address"/> with
/// user-specific metadata: a friendly label ("Home", "Work") and a default-address flag.
///
/// The backend enforces at most one default per user via application-layer logic in
/// the SetAsDefault command. (A unique filtered index is also acceptable; pick in migration.)
/// </summary>
public class SavedAddress : Auditable, ITenantEntity
{
    public string UserId { get; private set; }
    public User? User { get; private set; }

    public string AddressId { get; private set; }
    public Address? Address { get; private set; }

    [Required]
    [MaxLength(50)]
    public string Label { get; private set; }

    public bool IsDefault { get; private set; }

    public static SavedAddress Create(string userId, string addressId, string label, bool isDefault) =>
        new()
        {
            UserId = userId,
            AddressId = addressId,
            Label = label,
            IsDefault = isDefault,
        };

    public SavedAddress UpdateLabel(string label)
    {
        Label = label;
        return this;
    }

    public SavedAddress SetAddressId(string addressId)
    {
        AddressId = addressId;
        return this;
    }

    public SavedAddress SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        return this;
    }
}
