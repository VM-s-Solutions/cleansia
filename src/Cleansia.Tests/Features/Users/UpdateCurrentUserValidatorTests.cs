using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// Characterization of UpdateCurrentUser.Validator: pins every BusinessErrorMessage code it emits
/// (valid input passes; each rule fires) so the B3 base-class composition refactor — dropping the
/// BaseUserValidator base in favor of AbstractValidator + composed shared rules — stays
/// behavior-preserving. The moved first-name/last-name rules and the session-email-confirmation rule
/// (composed via UserEmailValidator) are part of the pinned contract.
/// </summary>
public class UpdateCurrentUserValidatorTests
{
    private const string UserEmail = "user@cleansia.cz";
    private const string UserId = "user-1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();

    private UpdateCurrentUser.Validator CreateValidator() => new(
        _userRepository.Object,
        _session.Object,
        _languageRepository.Object);

    private User ConfirmedUser()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        user.ConfirmEmail();
        user.Id = UserId;
        return user;
    }

    private void ArrangeOwner(bool confirmed = true)
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        if (confirmed)
        {
            user.ConfirmEmail();
        }

        user.Id = UserId;

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _userRepository
            .Setup(r => r.GetByEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    private void ArrangePhoneFree(string phone) =>
        _userRepository
            .Setup(r => r.GetByPhoneNumberAsync(phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

    private static UpdateCurrentUser.Command Valid() => new(
        Id: UserId,
        FirstName: "First",
        LastName: "Last",
        PhoneNumber: "+420123456789",
        BirthDate: null,
        Photo: null,
        LanguageCode: null);

    [Fact]
    public async Task Valid_Command_Passes()
    {
        ArrangeOwner();
        ArrangePhoneFree("+420123456789");

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Unconfirmed_Email_Fails_NotExistingUserWithEmail()
    {
        ArrangeOwner(confirmed: false);
        ArrangePhoneFree("+420123456789");

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotExistingUserWithEmail);
    }

    [Fact]
    public async Task Unresolvable_Session_Subject_Fails_NotAllowedToUpdateUser()
    {
        ArrangeOwner();
        ArrangePhoneFree("+420123456789");
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotAllowedToUpdateUser);
    }

    [Fact]
    public async Task Empty_FirstName_Fails_Required()
    {
        ArrangeOwner();
        ArrangePhoneFree("+420123456789");

        var result = await CreateValidator().ValidateAsync(Valid() with { FirstName = string.Empty });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateCurrentUser.Command.FirstName)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task FirstName_Too_Long_Fails_MaxLength()
    {
        ArrangeOwner();
        ArrangePhoneFree("+420123456789");

        var result = await CreateValidator().ValidateAsync(Valid() with { FirstName = new string('x', 51) });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateCurrentUser.Command.FirstName)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }

    [Fact]
    public async Task Empty_LastName_Fails_Required()
    {
        ArrangeOwner();
        ArrangePhoneFree("+420123456789");

        var result = await CreateValidator().ValidateAsync(Valid() with { LastName = string.Empty });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateCurrentUser.Command.LastName)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task LastName_Too_Long_Fails_MaxLength()
    {
        ArrangeOwner();
        ArrangePhoneFree("+420123456789");

        var result = await CreateValidator().ValidateAsync(Valid() with { LastName = new string('x', 51) });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateCurrentUser.Command.LastName)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }

    [Fact]
    public async Task Future_BirthDate_Fails_DateMustBeInPast()
    {
        ArrangeOwner();
        ArrangePhoneFree("+420123456789");

        var future = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        var result = await CreateValidator().ValidateAsync(Valid() with { BirthDate = future });

        Assert.Contains(result.Errors, e =>
            e.ErrorCode == nameof(UpdateCurrentUser.Command.BirthDate)
            && e.ErrorMessage == BusinessErrorMessage.DateMustBeInPast);
    }

    [Fact]
    public async Task Phone_Belongs_To_Another_User_Fails_ExistingPhoneNumber()
    {
        ArrangeOwner();
        var other = User.CreateWithPassword("other@cleansia.cz", "Password1", "O", "T");
        other.Id = "another-user";
        _userRepository
            .Setup(r => r.GetByPhoneNumberAsync("+420123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(other);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e =>
            e.ErrorCode == nameof(UpdateCurrentUser.Command.PhoneNumber)
            && e.ErrorMessage == BusinessErrorMessage.ExistingPhoneNumber);
    }

    [Fact]
    public async Task Avatar_Over_TenMebibytes_Fails_FileSizeExceeded()
    {
        ArrangeOwner();
        ArrangePhoneFree("+420123456789");

        var result = await CreateValidator().ValidateAsync(Valid() with { Photo = Avatar(TenMebibytes + 1024) });

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileSizeExceeded);
    }

    [Fact]
    public async Task Avatar_Under_TenMebibytes_Passes()
    {
        ArrangeOwner();
        ArrangePhoneFree("+420123456789");

        var result = await CreateValidator().ValidateAsync(Valid() with { Photo = Avatar(TenMebibytes - 1024) });

        Assert.True(result.IsValid);
    }

    private const long TenMebibytes = 10L * 1024 * 1024;

    private static BlobFileDto Avatar(long size)
    {
        var png = new byte[size];
        png[0] = 0x89;
        png[1] = 0x50;
        png[2] = 0x4E;
        png[3] = 0x47;
        return new BlobFileDto("avatar.png", Convert.ToBase64String(png), "image/png");
    }
}
