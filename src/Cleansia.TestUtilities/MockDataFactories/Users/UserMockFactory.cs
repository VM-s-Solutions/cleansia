using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Cleansia.TestUtilities.MockDataFactories.Users;

public class UserMockFactory
{
    public class UserPartial
    {
        [Password]
        [MaxLength(255)]
        public string? Password { get; set; }

        [Required]
        [MaxLength(50)]
        public string? FirstName { get; set; }

        [Required]
        [MaxLength(50)]
        public string? LastName { get; set; }

        [Required]
        [MaxLength(150)]
        [EmailAddress]
        public string? Email { get; set; }

        [PhoneNumber]
        [MaxLength(50)]
        public string? PhoneNumber { get; set; }

        [MaxLength(512)]
        public string? GoogleId { get; set; }

        public string? ResetPasswordCode { get; set; }

        public DateTimeOffset? ResetPasswordCodeExpiresAt { get; set; }

        [DateRangeControl(yearsRange: 100)]
        public DateOnly? BirthDate { get; set; }

        public UserProfile? Profile { get; set; }

        public AdminRole? AdminRole { get; set; }

        public AuthenticationType? AuthenticationType { get; set; }

        public string? CartId { get; set; }

        public string? ProfilePhotoName { get; set; }

        public string? ConfirmationCode { get; set; }

        public DateTimeOffset? ConfirmationCodeExpiresAt { get; set; }

        public int? FailedLoginAttempts { get; set; }

        public DateTimeOffset? LockoutEndsAt { get; set; }

        public int? ConfirmationCodeAttempts { get; set; }

        public int? ResetPasswordCodeAttempts { get; set; }

        public bool? IsEmailConfirmed { get; set; }

        public DateTime? CreatedAt { get; set; }
    }

    public static User Generate(UserPartial? mergeFrom = null)
    {
        // The profile and the role go through the factory rather than the merge so a mocked administrator
        // never carries the null role the constraint refuses; a mock that names no role is an Administrator.
        var profile = mergeFrom?.Profile ?? UserProfile.Customer;
        var adminRole = profile == UserProfile.Administrator
            ? mergeFrom?.AdminRole ?? AdminRole.Administrator
            : mergeFrom?.AdminRole;
        var user = User.CreateWithPassword(
            Constants.TestUserSession.TestUserEmail,
            Constants.TestUserSession.TestUserPassword,
            Constants.TestUserSession.TestFirstName,
            Constants.TestUserSession.TestLastName,
            profile,
            adminRole: adminRole);
        user.ConfirmEmail();
        user.Created(Constants.TestUserSession.TestUserId, mergeFrom?.CreatedAt ?? DateTime.UtcNow);

        return user.Merge(mergeFrom);
    }
}