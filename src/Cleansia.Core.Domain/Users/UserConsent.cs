using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Users;

public class UserConsent : Auditable, ITenantEntity
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
    /// The dated version string of the document accepted (ADR-0062 D4). Null on rows granted before
    /// versioning existed and on consent types that have no document.
    /// </summary>
    [MaxLength(32)]
    public string? DocumentVersion { get; private set; }

    public static UserConsent Grant(
        string userId,
        ConsentType consentType,
        string? ipAddress,
        string? userAgent,
        string? documentVersion)
        => new()
        {
            UserId = userId,
            ConsentType = consentType,
            IsGranted = true,
            GrantedAt = DateTimeOffset.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DocumentVersion = documentVersion
        };

    public UserConsent Withdraw()
    {
        IsGranted = false;
        WithdrawnAt = DateTimeOffset.UtcNow;
        return this;
    }

    public UserConsent Regrant(string? ipAddress, string? userAgent, string? documentVersion)
    {
        IsGranted = true;
        GrantedAt = DateTimeOffset.UtcNow;
        WithdrawnAt = null;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        DocumentVersion = documentVersion;
        return this;
    }

    /// <summary>
    /// A re-acceptance under a different document version on a row that is already granted. Versions
    /// are opaque strings, so "different" is all the row can tell; the row stays the truth about now and
    /// the audit trail is the history (ADR-0062 D4).
    /// </summary>
    public UserConsent AcceptVersion(string documentVersion, string? ipAddress, string? userAgent)
        => Regrant(ipAddress, userAgent, documentVersion);
}
