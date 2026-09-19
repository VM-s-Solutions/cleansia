namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// One administrator an admin event reaches, projected without the user graph: the id the feed row
/// is written to, and the address, name and language a per-recipient e-mail is rendered with.
/// </summary>
public sealed record AdministratorRecipient(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PreferredLanguageCode);
