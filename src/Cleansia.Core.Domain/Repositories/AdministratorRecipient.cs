using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// One administrator an admin event may reach, projected without the user graph: the id the feed row
/// is written to, the address, name and language a per-recipient e-mail is rendered with, and the role
/// the event's audience is matched against.
/// </summary>
public sealed record AdministratorRecipient(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PreferredLanguageCode,
    AdminRole? AdminRole);
