using Cleansia.Core.AppServices.Auditing;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// What a <c>customer.session.login</c> row records: how the caller proved who they are, the lifetime
/// the session was issued with, and the audience the token was minted for. Top-level because the
/// password login on each customer host and the two social sign-ins all emit it.
/// <see cref="EmailConfirmed"/> is false on the one success that opens no session — the password was
/// right but the address is unconfirmed, so the token is withheld and the client is sent to confirm.
/// </summary>
public record LoginEvidence(
    string Method,
    bool RememberMe,
    string ClientAudience,
    bool EmailConfirmed) : ICustomerAuditPayload
{
    public const string PasswordMethod = "Password";
    public const string GoogleMethod = "Google";
    public const string AppleMethod = "Apple";
}
