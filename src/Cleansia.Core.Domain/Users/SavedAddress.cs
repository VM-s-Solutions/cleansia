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

    /// <summary>
    /// Floor and door for this user at this address. On the SAVED address, not on
    /// <see cref="Address"/>, for the same reason the order carries its own copy:
    /// the Address row is deduped across every user in the building.
    /// </summary>
    [MaxLength(20)]
    public string? Floor { get; private set; }

    /// <inheritdoc cref="Floor"/>
    [MaxLength(20)]
    public string? Apartment { get; private set; }

    public static SavedAddress Create(
        string userId,
        string addressId,
        string label,
        bool isDefault,
        string? floor = null,
        string? apartment = null) =>
        new()
        {
            UserId = userId,
            AddressId = addressId,
            Label = label,
            IsDefault = isDefault,
            Floor = string.IsNullOrWhiteSpace(floor) ? null : floor.Trim(),
            Apartment = string.IsNullOrWhiteSpace(apartment) ? null : apartment.Trim(),
        };

    public SavedAddress UpdateUnit(string? floor, string? apartment)
    {
        Floor = string.IsNullOrWhiteSpace(floor) ? null : floor.Trim();
        Apartment = string.IsNullOrWhiteSpace(apartment) ? null : apartment.Trim();
        return this;
    }

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
