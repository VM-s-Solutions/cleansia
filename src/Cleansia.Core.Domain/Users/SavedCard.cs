using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Users;

/// <summary>
/// A card a customer saves as the guarantee for a cash booking (owner ruling 2026-09-28, decision 16):
/// captured through a Stripe SetupIntent on the customer's Stripe Customer for the currency, never
/// charged at capture, and saved under consent that a cancellation fee, a lockout fee, unpaid cash and an
/// approved top-up may be charged to it. The row is written when the capture starts, with the consent
/// evidence; the card itself lands when Stripe reports the SetupIntent succeeded. Removal deactivates it.
/// </summary>
public class SavedCard : TenantAuditable
{
    /// <summary>
    /// The consent wording the clients show when a card is saved. A draft until the lawyer's wording
    /// arrives; bump it with every change to that wording, so each card records the text it was saved under.
    /// </summary>
    public const string ConsentTextVersionInForce = "card-guarantee-draft-2026-09-28";

    [Required]
    [MaxLength(26)]
    public string UserId { get; private set; } = default!;
    public User? User { get; private set; }

    [Required]
    [MaxLength(26)]
    public string CurrencyId { get; private set; } = default!;
    public Currency? Currency { get; private set; }

    [Required]
    [MaxLength(64)]
    public string StripeCustomerId { get; private set; } = default!;

    [MaxLength(64)]
    public string? StripePaymentMethodId { get; private set; }

    [MaxLength(32)]
    public string? Brand { get; private set; }

    [MaxLength(4)]
    public string? Last4 { get; private set; }

    public int? ExpMonth { get; private set; }

    public int? ExpYear { get; private set; }

    public DateTimeOffset? CapturedOn { get; private set; }

    [Required]
    [MaxLength(64)]
    public string ConsentTextVersion { get; private set; } = default!;

    public DateTimeOffset ConsentedOn { get; private set; }

    [MaxLength(45)]
    public string? ConsentIpAddress { get; private set; }

    [MaxLength(120)]
    public string? ConsentDeviceLabel { get; private set; }

    private SavedCard()
    {
    }

    public bool IsCaptured => CapturedOn is not null;

    public static SavedCard Start(
        string userId,
        string currencyId,
        string stripeCustomerId,
        string? consentIpAddress,
        string? consentDeviceLabel) =>
        new()
        {
            UserId = userId,
            CurrencyId = currencyId,
            StripeCustomerId = stripeCustomerId,
            ConsentTextVersion = ConsentTextVersionInForce,
            ConsentedOn = DateTimeOffset.UtcNow,
            ConsentIpAddress = consentIpAddress,
            ConsentDeviceLabel = consentDeviceLabel,
        };

    public void Capture(string stripePaymentMethodId, string brand, string last4, int expMonth, int expYear)
    {
        StripePaymentMethodId = stripePaymentMethodId;
        Brand = brand;
        Last4 = last4;
        ExpMonth = expMonth;
        ExpYear = expYear;
        CapturedOn = DateTimeOffset.UtcNow;
    }
}
