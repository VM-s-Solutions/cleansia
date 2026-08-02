using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The single write path for <see cref="Cleansia.Core.Domain.Users.UserConsent"/>. Every surface that
/// captures a consent (the GDPR consent endpoints, the partner-onboarding checkbox) goes through here
/// so the evidentiary fields — IP, device, timestamp — are stamped the same way everywhere.
/// </summary>
public interface IConsentService
{
    /// <summary>
    /// Records the grant, or re-grants a previously withdrawn one. Returns <c>false</c> when the user
    /// already holds this consent and nothing was written — the caller decides whether that is an
    /// error (an explicit grant request) or a no-op (re-saving an onboarding form).
    /// </summary>
    Task<bool> TryGrantAsync(string userId, ConsentType consentType, CancellationToken cancellationToken);
}
