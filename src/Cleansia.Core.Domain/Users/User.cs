using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Attributes;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Users;

public class User : Auditable, ITenantEntity
{
    [Password]
    [MaxLength(255)]
    public string? Password { get; private set; }

    [Required]
    [MaxLength(50)]
    public string FirstName { get; private set; }

    [Required]
    [MaxLength(50)]
    public string LastName { get; private set; }

    [Required]
    [MaxLength(150)]
    [EmailAddress]
    public string Email { get; private set; }

    [PhoneNumber]
    [MaxLength(50)]
    public string? PhoneNumber { get; private set; }

    [MaxLength(512)]
    public string? GoogleId { get; private set; }

    public string? ResetPasswordCode { get; private set; }

    public DateTimeOffset? ResetPasswordCodeExpiresAt { get; private set; }

    [DateRangeControl(yearsRange: 100)]
    public DateOnly? BirthDate { get; private set; }

    public UserProfile Profile { get; private set; } = UserProfile.Customer;

    public AuthenticationType AuthenticationType { get; private set; } = AuthenticationType.Internal;

    public string? ProfilePhotoName { get; private set; }

    /// <summary>
    /// SHA-256 HASH of the email-confirmation token (never the raw value):
    /// the raw token is emailed once via <see cref="RawConfirmationToken"/> and only the hash is
    /// persisted, so a stolen DB row cannot confirm/log in the account.
    /// </summary>
    public string? ConfirmationCode { get; private set; }

    public DateTimeOffset? ConfirmationCodeExpiresAt { get; private set; }

    /// <summary>
    /// Transient (NOT persisted) carrier for the RAW confirmation token produced by
    /// <see cref="CreateWithPassword"/>, so the registration handler can email it. The persisted
    /// column (<see cref="ConfirmationCode"/>) only ever holds the hash. The token-refresh paths
    /// (<see cref="UpdateConfirmationCode"/> / <see cref="UpdateResetPasswordToken"/>) instead RETURN
    /// the raw token directly.
    /// </summary>
    [NotMapped]
    public string? RawConfirmationToken { get; private set; }

    public bool IsEmailConfirmed { get; private set; }

    [MaxLength(5)]
    public string? PreferredLanguageCode { get; private set; }

    public Language? PreferredLanguage { get; private set; }

    /// <summary>
    /// Persistent Stripe Customer id. Created lazily on the user's first
    /// card-paying booking, then reused forever after to support saved
    /// payment methods (PaymentSheet) and (future) subscriptions. Cash-only
    /// users never get one.
    /// </summary>
    [MaxLength(64)]
    public string? StripeCustomerId { get; private set; }

    public Cart? Cart { get; private set; }

    public Employee? Employee { get; private set; }

    private ICollection<Order> _orders = [];
    public virtual IReadOnlyCollection<Order> Orders => _orders.ToList().AsReadOnly();

    private ICollection<UserMembership> _memberships = [];
    public virtual IReadOnlyCollection<UserMembership> Memberships => _memberships.ToList().AsReadOnly();

    /// <summary>
    /// The user's currently-providing-benefits membership, or null. Reads from
    /// the in-memory collection — caller must ensure the navigation is loaded
    /// (Include or explicit load) before relying on this.
    /// </summary>
    public UserMembership? ActiveMembership => _memberships.FirstOrDefault(m => m.IsActive);

    public static User CreateWithPassword(string email, string password, string firstName, string lastName, UserProfile profile = UserProfile.Customer, string? languageCode = null)
    {
        // Generate a cryptographic raw token, persist only its hash, and surface
        // the raw value transiently so the registration handler can email it (never persisted/logged).
        var rawConfirmationToken = SecurityTokens.Generate();

        return new()
        {
            Email = email,
            Password = password,
            FirstName = firstName,
            LastName = lastName,
            PreferredLanguageCode = languageCode ?? "en",
            ConfirmationCode = SecurityTokens.Hash(rawConfirmationToken),
            RawConfirmationToken = rawConfirmationToken,
            ConfirmationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Profile = profile,
        };
    }

    public static User CreateWithGoogle(string email, string firstName, string lastName, string googleId, string? languageCode = null)
        => new()
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            AuthenticationType = AuthenticationType.Google,
            GoogleId = googleId,
            PreferredLanguageCode = languageCode ?? "en",
            IsEmailConfirmed = true
        };

    /// <summary>
    /// Generates a fresh cryptographic password-reset token, persists only its hash, and RETURNS the
    /// RAW token so the caller can email it. The raw value is never stored.
    /// </summary>
    public string UpdateResetPasswordToken()
    {
        var rawToken = SecurityTokens.Generate();
        ResetPasswordCode = SecurityTokens.Hash(rawToken);
        ResetPasswordCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
        return rawToken;
    }

    public User UpdatePassword(string password)
    {
        Password = password;
        return this;
    }

    public User ClearResetPasswordToken()
    {
        ResetPasswordCode = null;
        ResetPasswordCodeExpiresAt = null;
        return this;
    }

    public User Update(string firstName, string lastName, string phoneNumber, DateOnly? birthDate = null)
    {
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        BirthDate = birthDate;

        return this;
    }

    public User UpgradeToEmployee()
    {
        if (Profile == UserProfile.Customer)
            Profile = UserProfile.Employee;

        return this;
    }

    public User UpdateProfilePhotoName(string? profilePhotoName)
    {
        ProfilePhotoName = profilePhotoName;

        return this;
    }

    public User ConfirmEmail()
    {
        // One-shot: clear the hashed token so a consumed confirmation cannot be replayed.
        ConfirmationCode = null;
        ConfirmationCodeExpiresAt = null;
        RawConfirmationToken = null;
        IsEmailConfirmed = true;

        return this;
    }

    public User UpdatePhoneNumber(string phoneNumber)
    {
        PhoneNumber = phoneNumber;

        return this;
    }

    /// <summary>
    /// Generates a fresh cryptographic email-confirmation token, persists only its hash, and RETURNS
    /// the RAW token so the caller can email it. The raw value is never stored.
    /// </summary>
    public string UpdateConfirmationCode()
    {
        var rawToken = SecurityTokens.Generate();
        ConfirmationCode = SecurityTokens.Hash(rawToken);
        RawConfirmationToken = rawToken;
        ConfirmationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);

        return rawToken;
    }

    public User UpdateLanguagePreference(string? languageCode)
    {
        PreferredLanguageCode = languageCode;
        return this;
    }

    /// <summary>
    /// Set the Stripe Customer id once it's been created. Idempotent: callers
    /// should check <see cref="StripeCustomerId"/> first and only call this
    /// when transitioning from null → first card payment.
    /// </summary>
    public User AssignStripeCustomerId(string stripeCustomerId)
    {
        StripeCustomerId = stripeCustomerId;
        return this;
    }

    public User Anonymize()
    {
        FirstName = AnonymizationMarker.Value;
        LastName = AnonymizationMarker.Value;
        Email = $"deleted_{Id}@anonymized.local";
        PhoneNumber = null;
        BirthDate = null;
        GoogleId = null;
        Password = null;
        ProfilePhotoName = null;
        PreferredLanguageCode = null;
        ResetPasswordCode = null;
        ResetPasswordCodeExpiresAt = null;
        ConfirmationCode = null;
        ConfirmationCodeExpiresAt = null;
        RawConfirmationToken = null;
        StripeCustomerId = null;
        return this;
    }
}
