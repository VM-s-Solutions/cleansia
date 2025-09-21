using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Orders;
using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Users;

public class User : Auditable
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

    public string? ConfirmationCode { get; private set; }

    public DateTimeOffset? ConfirmationCodeExpiresAt { get; private set; }

    public bool IsEmailConfirmed { get; private set; }

    public string CartId { get; private set; }
    public Cart? Cart { get; private set; }

    public string EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }

    private ICollection<Order> _orders = [];
    public virtual IReadOnlyCollection<Order> Orders => _orders.ToList().AsReadOnly();

    public static User CreateWithPassword(string email, string password, string firstName, string lastName, UserProfile profile = UserProfile.Customer)
        => new()
        {
            Email = email,
            Password = password,
            FirstName = firstName,
            LastName = lastName,
            ConfirmationCode = new Random().Next(100000, 999999).ToString(),
            ConfirmationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Profile = profile,
        };

    public static User CreateWithGoogle(string email, string firstName, string lastName, string googleId)
        => new()
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            AuthenticationType = AuthenticationType.Google,
            GoogleId = googleId,
            IsEmailConfirmed = true
        };

    public User UpdateResetPasswordToken()
    {
        ResetPasswordCode = new Random().Next(100000, 999999).ToString();
        ResetPasswordCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
        return this;
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

    public User UpdateProfilePhotoName(string? profilePhotoName)
    {
        ProfilePhotoName = profilePhotoName;

        return this;
    }

    public User ConfirmEmail()
    {
        ConfirmationCode = null;
        ConfirmationCodeExpiresAt = null;
        IsEmailConfirmed = true;

        return this;
    }

    public User UpdatePhoneNumber(string phoneNumber)
    {
        PhoneNumber = phoneNumber;

        return this;
    }

    public User UpdateConfirmationCode()
    {
        ConfirmationCode = new Random().Next(100000, 999999).ToString();
        ConfirmationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);

        return this;
    }
}
