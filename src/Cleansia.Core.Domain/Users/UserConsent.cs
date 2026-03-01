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

    public static UserConsent Grant(string userId, ConsentType consentType, string? ipAddress, string? userAgent)
        => new()
        {
            UserId = userId,
            ConsentType = consentType,
            IsGranted = true,
            GrantedAt = DateTimeOffset.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

    public UserConsent Withdraw()
    {
        IsGranted = false;
        WithdrawnAt = DateTimeOffset.UtcNow;
        return this;
    }

    public UserConsent Regrant(string? ipAddress, string? userAgent)
    {
        IsGranted = true;
        GrantedAt = DateTimeOffset.UtcNow;
        WithdrawnAt = null;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        return this;
    }
}
