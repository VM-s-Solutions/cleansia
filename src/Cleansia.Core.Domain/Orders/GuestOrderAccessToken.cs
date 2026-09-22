using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// The credential a guest proves a booking with. Stored as a SHA-256 hash — the raw token is returned
/// to the issuing caller exactly once, e-mailed, and never retrievable again (the same contract as
/// <see cref="Users.RefreshToken"/> and the account confirmation token).
///
/// <para>It replaces the (display number, e-mail, confirmation code) triple, which was not a secret:
/// the display number is sequential, the e-mail is not private, and the confirmation code was served
/// to every assigned cleaner on the order detail — so a cleaner could cancel their customer's booking
/// and charge them the cancellation tier.</para>
///
/// <para>Durable, not single-use: the guest tracks the booking with it and cancels with it. It dies
/// either at <see cref="ExpiresOn"/> — <see cref="LifetimeDaysAfterCleaning"/> days past the cleaning,
/// which covers the refund window and the month the receipt belongs to — or when the booking is
/// cancelled and there is nothing left to do with it.</para>
/// </summary>
public class GuestOrderAccessToken : TenantAuditable
{
    /// <summary>
    /// How long past the cleaning the credential survives. Long enough to reach the 14-day refund
    /// window and to ask about a receipt afterwards; short enough that a mailbox read years later is
    /// not a live key to somebody's booking.
    /// </summary>
    public const int LifetimeDaysAfterCleaning = 30;

    public string OrderId { get; private set; } = default!;

    public Order? Order { get; private set; }

    /// <summary>SHA-256 hex digest of the raw token. Indexed unique; the raw value is never stored.</summary>
    [Required]
    [MaxLength(64)]
    public string TokenHash { get; private set; } = default!;

    public DateTimeOffset ExpiresOn { get; private set; }

    public DateTimeOffset? RevokedOn { get; private set; }

    /// <summary>
    /// Transient (NOT persisted) carrier for the RAW token produced by <see cref="Issue"/>, so the
    /// issuing caller can e-mail it. Mirrors <c>User.RawConfirmationToken</c>.
    /// </summary>
    [NotMapped]
    public string? RawToken { get; private set; }

    public bool IsLive(DateTimeOffset now) => RevokedOn is null && ExpiresOn > now;

    public static GuestOrderAccessToken Issue(string orderId, DateTimeOffset expiresOn)
    {
        var rawToken = SecurityTokens.Generate(SecurityTokens.DurableTokenByteLength);

        return new()
        {
            OrderId = orderId,
            TokenHash = SecurityTokens.Hash(rawToken),
            ExpiresOn = expiresOn,
            RawToken = rawToken,
        };
    }

    public static DateTimeOffset ExpiryFor(DateTime cleaningDateTime) =>
        new DateTimeOffset(DateTime.SpecifyKind(cleaningDateTime, DateTimeKind.Utc))
            .AddDays(LifetimeDaysAfterCleaning);

    public GuestOrderAccessToken Revoke(DateTimeOffset at)
    {
        RevokedOn ??= at;
        return this;
    }
}
