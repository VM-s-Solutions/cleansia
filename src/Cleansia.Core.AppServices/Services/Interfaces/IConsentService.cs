using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The single write path for <see cref="Cleansia.Core.Domain.Users.UserConsent"/>. Every surface that
/// captures a consent (the GDPR consent endpoints, registration, the partner-onboarding checkbox) goes
/// through here so the evidentiary fields — IP, device, timestamp, document version — are stamped the
/// same way everywhere.
/// </summary>
public interface IConsentService
{
    /// <summary>
    /// Records the grant, re-grants a previously withdrawn one, or moves an already-granted row to
    /// <paramref name="documentVersion"/> when it differs from the version the row holds. Returns
    /// <c>false</c> when the user already holds this consent under that version and nothing was written
    /// — the caller decides whether that is an error (an explicit grant request) or a no-op (re-saving
    /// an onboarding form). <paramref name="documentVersion"/> is the version of the legal text in force
    /// for THIS subject: null for a consent that has no document, and null for an employee, who accepts
    /// a different text than the customer constants describe (ADR-0041, ADR-0062 D4).
    /// </summary>
    Task<bool> TryGrantAsync(string userId, ConsentType consentType, string? documentVersion, CancellationToken cancellationToken);
}
