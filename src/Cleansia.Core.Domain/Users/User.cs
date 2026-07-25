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
    public const int MaxFailedLoginAttempts = 5;
    public const int MaxCodeVerificationAttempts = 5;
    public static readonly TimeSpan FailedLoginLockout = TimeSpan.FromMinutes(15);

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

    [MaxLength(512)]
    public string? AppleId { get; private set; }

    public string? ResetPasswordCode { get; private set; }

    public DateTimeOffset? ResetPasswordCodeExpiresAt { get; private set; }

    [DateRangeControl(yearsRange: 100)]
    public DateOnly? BirthDate { get; private set; }

    public UserProfile Profile { get; private set; } = UserProfile.Customer;

    public AuthenticationType AuthenticationType { get; private set; } = AuthenticationType.Internal;

    public string? ProfilePhotoName { get; private set; }

    /// <summary>
    /// SHA-256 HASH of the email-confirmation code (never the raw value): the raw 6-digit OTP is
    /// emailed once via <see cref="RawConfirmationToken"/> and only the hash is persisted. For a
    /// 6-digit space the at-rest hash is hygiene, not brute-proofing — the safety is the 15-minute
    /// expiry + the per-code attempt budget + email-scoped verification (never lookup by bare code).
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

    // The failure-path increments live in UserRepository as atomic conditional UPDATEs (a failing
    // login/confirm command never reaches the UnitOfWork commit, and a read-then-write counter would
    // race under a distributed guessing run). The entity owns only the read side and the
    // success-path / re-issuance resets.
    public int FailedLoginAttempts { get; private set; }

    public DateTimeOffset? LockoutEndsAt { get; private set; }

    public int ConfirmationCodeAttempts { get; private set; }

    public int ResetPasswordCodeAttempts { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

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
        // Generate the typed 6-digit verification code (the apps render six digit boxes), persist only
        // its hash, and surface the raw value transiently so the registration handler can email it
        // (never persisted/logged). The password-reset token stays the 128-bit link token.
        var rawConfirmationToken = SecurityTokens.GenerateOtp();

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

    public static User CreateWithApple(string email, string firstName, string lastName, string appleSub, string? languageCode = null)
        => new()
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            AuthenticationType = AuthenticationType.Apple,
            AppleId = appleSub,
            PreferredLanguageCode = languageCode ?? "en",
            IsEmailConfirmed = true
        };

    /// <summary>
    /// Generates a fresh 6-digit password-reset OTP (same shape as the email-confirmation code — the apps
    /// render six digit boxes), persists only its hash, and RETURNS the RAW code so the caller can email it.
    /// The raw value is never stored. Resolution is always (email + code) under the attempt budget + 15-min
    /// expiry below, so a 6-digit space is safe against brute force (a bare-code lookup is never allowed).
    /// </summary>
    public string UpdateResetPasswordToken()
    {
        var rawToken = SecurityTokens.GenerateOtp();
        ResetPasswordCode = SecurityTokens.Hash(rawToken);
        ResetPasswordCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
        ResetPasswordCodeAttempts = 0;
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
        ResetPasswordCodeAttempts = 0;
        return this;
    }

    public bool IsLockedOut(DateTimeOffset now)
        => LockoutEndsAt.HasValue && LockoutEndsAt.Value > now;

    public User ResetLoginThrottle()
    {
        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
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

    /// <summary>
    /// Binds the VERIFIED Apple subject to an account that does not carry one yet, and does nothing at all
    /// when one is already stored.
    /// <para>
    /// Why this matters: Apple sends the email only on the FIRST authorization and the sub on every one, so
    /// an Apple account whose row predates sub-storage is reachable by the email fallback exactly once —
    /// and on every sign-in after that, the sub lookup misses and there is no email to fall back to, which
    /// locks the user out of their own account with no self-service recovery. Binding the sub on the one
    /// sign-in that still carries an email closes that window permanently.
    /// </para>
    /// <para>
    /// The never-overwrite rule is a SECURITY property, not tidiness: an existing sub is the account's
    /// verified identity anchor, and letting a later sign-in rewrite it would let one Apple ID take over an
    /// account anchored to a different one (S1 server-truth-identity).
    /// </para>
    /// </summary>
    public User LinkAppleId(string appleSub)
    {
        if (string.IsNullOrWhiteSpace(AppleId) && !string.IsNullOrWhiteSpace(appleSub))
        {
            AppleId = appleSub.Trim();
        }

        return this;
    }

    /// <summary>
    /// Fills ONLY the blank name parts and leaves anything already stored untouched. The values reaching
    /// it are external-identity ones (Apple replays the name captured at the FIRST authorization), so they
    /// can be stale by years — they may repair a blank field but must never silently undo a name the user
    /// has since edited in the profile screen. Displacing a name is the strictly narrower
    /// <see cref="ReplaceSystemGeneratedName"/>.
    /// </summary>
    public User FillMissingName(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(firstName))
        {
            FirstName = firstName.Trim();
        }

        if (string.IsNullOrWhiteSpace(LastName) && !string.IsNullOrWhiteSpace(lastName))
        {
            LastName = lastName.Trim();
        }

        return this;
    }

    /// <summary>
    /// Promotes a name the external provider GENUINELY supplied over one WE generated, per part.
    /// <paramref name="firstName"/>/<paramref name="lastName"/> are the parts the client actually sent;
    /// <paramref name="derivedFirstName"/>/<paramref name="derivedLastName"/> are what our own
    /// email-derivation would produce for this account, and are the only evidence that a stored value is
    /// system-generated rather than user-authored.
    /// <para>
    /// Why this exists: Apple hands the real name over exactly ONCE, on the first authorization. If that
    /// one payload lands on an account already showing a derived placeholder, a blank-parts-only back-fill
    /// silently discards it and the placeholder outlives the only chance to correct it — the user then has
    /// to notice and retype their own name. So a part is overwritten only when it is blank OR equal to the
    /// derivation (provably ours); a part the user typed themselves is never touched, and a part with
    /// nothing genuine to replace it is left alone.
    /// </para>
    /// <para>
    /// The derivation and its fallback constants are therefore part of an IDENTIFICATION rule, not just a
    /// default: changing them orphans every row already written with the old values, which then reads as
    /// user-authored forever. Do not change them casually.
    /// </para>
    /// </summary>
    public User ReplaceSystemGeneratedName(string? firstName, string? lastName, string? derivedFirstName, string? derivedLastName)
    {
        if (IsSystemGenerated(FirstName, derivedFirstName) && !string.IsNullOrWhiteSpace(firstName))
        {
            FirstName = firstName.Trim();
        }

        if (IsSystemGenerated(LastName, derivedLastName) && !string.IsNullOrWhiteSpace(lastName))
        {
            LastName = lastName.Trim();
        }

        return FillMissingName(derivedFirstName, derivedLastName);
    }

    private static bool IsSystemGenerated(string? storedNamePart, string? derivedNamePart)
        => string.IsNullOrWhiteSpace(storedNamePart)
            || (!string.IsNullOrWhiteSpace(derivedNamePart)
                && string.Equals(storedNamePart.Trim(), derivedNamePart.Trim(), StringComparison.OrdinalIgnoreCase));

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
        ConfirmationCodeAttempts = 0;
        RawConfirmationToken = null;
        IsEmailConfirmed = true;

        return this;
    }

    public User UpdatePhoneNumber(string phoneNumber)
    {
        PhoneNumber = phoneNumber;

        return this;
    }

    public User UpdateBirthDate(DateOnly? birthDate)
    {
        BirthDate = birthDate;

        return this;
    }

    /// <summary>
    /// Generates a fresh 6-digit email-confirmation OTP, persists only its hash, and RETURNS
    /// the RAW code so the caller can email it. The raw value is never stored. Re-issuance resets the
    /// attempt budget — a user who exhausted it recovers by requesting a new code.
    /// </summary>
    public string UpdateConfirmationCode()
    {
        var rawToken = SecurityTokens.GenerateOtp();
        ConfirmationCode = SecurityTokens.Hash(rawToken);
        RawConfirmationToken = rawToken;
        ConfirmationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
        ConfirmationCodeAttempts = 0;

        return rawToken;
    }

    public User UpdateLanguagePreference(string? languageCode)
    {
        PreferredLanguageCode = languageCode;
        return this;
    }

    public User RecordLogin(DateTimeOffset at)
    {
        LastLoginAt = at;
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
        AppleId = null;
        Password = null;
        ProfilePhotoName = null;
        PreferredLanguageCode = null;
        ResetPasswordCode = null;
        ResetPasswordCodeExpiresAt = null;
        ResetPasswordCodeAttempts = 0;
        ConfirmationCode = null;
        ConfirmationCodeExpiresAt = null;
        ConfirmationCodeAttempts = 0;
        RawConfirmationToken = null;
        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
        StripeCustomerId = null;
        return this;
    }
}
