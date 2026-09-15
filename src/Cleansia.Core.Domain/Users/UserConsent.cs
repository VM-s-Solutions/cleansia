using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.Domain.Users;

public class UserConsent : TenantAuditable
{
    [Required]
    public string UserId { get; private set; }

    public User? User { get; private set; }

    public ConsentType ConsentType { get; private set; }

    public bool IsGranted { get; private set; }

    public DateTimeOffset? GrantedAt { get; private set; }

    public DateTimeOffset? WithdrawnAt { get; private set; }

    [MaxLength(45)]
    public string? IpAddress { get; private set; }

    [MaxLength(500)]
    public string? UserAgent { get; private set; }

    /// <summary>
    /// The version of the document accepted — its effective date, <c>yyyy-MM-dd</c> (ADR-0062 D4). Null
    /// on rows granted before versioning existed and on consent types that have no document.
    /// </summary>
    [MaxLength(32)]
    public string? DocumentVersion { get; private set; }

    /// <summary>The stored text the version names; null where <see cref="DocumentVersion"/> is.</summary>
    [MaxLength(26)]
    public string? LegalDocumentId { get; private set; }

    public LegalDocument? LegalDocument { get; private set; }

    public static UserConsent Grant(
        string userId,
        ConsentType consentType,
        string? ipAddress,
        string? userAgent,
        string? documentVersion,
        string? legalDocumentId = null)
        => new()
        {
            UserId = userId,
            ConsentType = consentType,
            IsGranted = true,
            GrantedAt = DateTimeOffset.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DocumentVersion = documentVersion,
            LegalDocumentId = legalDocumentId
        };

    public UserConsent Withdraw()
    {
        IsGranted = false;
        WithdrawnAt = DateTimeOffset.UtcNow;
        return this;
    }

    public UserConsent Regrant(string? ipAddress, string? userAgent, string? documentVersion, string? legalDocumentId = null)
    {
        IsGranted = true;
        GrantedAt = DateTimeOffset.UtcNow;
        WithdrawnAt = null;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        DocumentVersion = documentVersion;
        LegalDocumentId = legalDocumentId;
        return this;
    }

    /// <summary>
    /// A re-acceptance of a different document on a row that is already granted — different, not newer:
    /// the row stays the truth about now and the audit trail is the history (ADR-0062 D4).
    /// </summary>
    public UserConsent AcceptVersion(string documentVersion, string? ipAddress, string? userAgent, string? legalDocumentId = null)
        => Regrant(ipAddress, userAgent, documentVersion, legalDocumentId);
}
