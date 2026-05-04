using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Loyalty;

/// <summary>
/// One-per-user, lifetime referral code (e.g. <c>X3K9P2</c>). Generated lazily
/// on first read of the customer's referral panel. Stored uppercase; the
/// alphabet excludes I/1, O/0, and vowels to avoid accidental words and
/// look-alike characters. Codespace at 6 chars from a 28-char alphabet is
/// ~481M, more than enough headroom for collision-retry generation.
/// </summary>
public class ReferralCode : Auditable, ITenantEntity
{
    [Required]
    public string UserId { get; private set; } = default!;
    public User? User { get; private set; }

    [Required]
    [MaxLength(10)]
    public string Code { get; private set; } = default!;

    /// <summary>
    /// Denormalised count of qualified referrals — bumped by
    /// <see cref="RecordUse"/> when an invitee completes their first order.
    /// </summary>
    [Required]
    public int TimesUsed { get; private set; }

    // IsActive is inherited from BaseEntity (public set so EF + service can flip it).

    // Private constructor for EF Core
    private ReferralCode() { }

    /// <summary>
    /// Factory used by <c>ReferralService</c>. The service supplies the
    /// already-generated unique code (it owns the collision-retry loop) and
    /// the actor for audit trail.
    /// </summary>
    public static ReferralCode Generate(string userId, string code, string actorId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required", nameof(userId));
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required", nameof(code));
        }

        var rc = new ReferralCode
        {
            UserId = userId,
            Code = code.Trim().ToUpperInvariant(),
            IsActive = true,
            TimesUsed = 0,
        };
        rc.Created(actorId, DateTimeOffset.UtcNow);
        return rc;
    }

    /// <summary>
    /// Increment the denormalised "qualified referrals" counter so the
    /// inviter's "X friends qualified" stat stays O(1) to read.
    /// </summary>
    public void RecordUse(string actorId)
    {
        TimesUsed += 1;
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    public void Disable(string actorId)
    {
        IsActive = false;
        Updated(actorId, DateTimeOffset.UtcNow);
    }
}
