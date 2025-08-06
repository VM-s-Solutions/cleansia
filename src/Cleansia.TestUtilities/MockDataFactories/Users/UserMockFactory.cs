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

        public AuthenticationType? AuthenticationType { get; set; }

        public string? CartId { get; set; }

        public string? ProfilePhotoName { get; set; }

        public string? ConfirmationCode { get; set; }

        public DateTimeOffset? ConfirmationCodeExpiresAt { get; set; }

        public bool? IsEmailConfirmed { get; set; }

        public DateTime? CreatedAt { get; set; }
    }

    public static User Generate(UserPartial? mergeFrom = null)
    {
        var user = User.CreateWithPassword(
            Constants.TestUserSession.TestUserEmail,
            Constants.TestUserSession.TestUserPassword,
            Constants.TestUserSession.TestFirstName,
            Constants.TestUserSession.TestLastName);
        user.ConfirmEmail();
        user.Created(Constants.TestUserSession.TestUserId, mergeFrom?.CreatedAt ?? DateTime.UtcNow);

        return user.Merge(mergeFrom);
    }
}