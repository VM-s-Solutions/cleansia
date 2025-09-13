using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Auth;

public class ResendConfirmationEmailValidatorTests
{
    [Fact]
    public async Task When_Email_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockUserRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        var validator = new ResendConfirmationEmail.Validator(mockUserRepo.Object, mockLangRepo.Object);
        var command = new ResendConfirmationEmail.Command(null, "CZ");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        var error = result.Errors[0];
        Assert.Equal("Email", error.PropertyName);
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal("Email", error.ErrorCode);
    }

    [Fact]
    public async Task When_Email_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockUserRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        var validator = new ResendConfirmationEmail.Validator(mockUserRepo.Object, mockLangRepo.Object);
        var command = new ResendConfirmationEmail.Command("", "CZ");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        var error = result.Errors[0];
        Assert.Equal("Email", error.PropertyName);
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal("Email", error.ErrorCode);
    }

    [Fact]
    public async Task When_Email_Does_Not_Exist_Then_Validation_Fails_With_NotExistingUser_Error()
    {
        // Arrange
        var email = "test@example.com";
        var mockUserRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockUserRepo.Setup(r => r.ExistsWithEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new ResendConfirmationEmail.Validator(mockUserRepo.Object, mockLangRepo.Object);
        var command = new ResendConfirmationEmail.Command(email, "CZ");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        var error = result.Errors[0];
        Assert.Equal("Email", error.PropertyName);
        Assert.Equal(BusinessErrorMessage.NotExistingUserWithEmail, error.ErrorMessage);
        Assert.Equal("Email", error.ErrorCode);
    }

    [Fact]
    public async Task When_Email_Exists_But_HasConfirmedMail_Then_Validation_Fails_With_EmailConfirmed_Error()
    {
        // Arrange
        var email = "test@example.com";
        var mockUserRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockUserRepo.Setup(r => r.ExistsWithEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mockUserRepo.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(UserMockFactory.Generate());
        var validator = new ResendConfirmationEmail.Validator(mockUserRepo.Object, mockLangRepo.Object);
        var command = new ResendConfirmationEmail.Command(email, "CZ");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        var error = result.Errors[0];
        Assert.Equal("Email", error.PropertyName);
        Assert.Equal(BusinessErrorMessage.EmailConfirmed, error.ErrorMessage);
        Assert.Equal("Email", error.ErrorCode);
    }

    [Fact]
    public async Task When_Email_Exists_Then_Validation_Passes()
    {
        // Arrange
        var email = "test@example.com";
        var mockUserRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockUserRepo.Setup(r => r.ExistsWithEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mockUserRepo.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(User.CreateWithPassword(
            TestUtilities.Constants.TestUserSession.TestUserEmail,
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            TestUtilities.Constants.TestUserSession.TestFirstName,
            TestUtilities.Constants.TestUserSession.TestLastName));
        var validator = new ResendConfirmationEmail.Validator(mockUserRepo.Object, mockLangRepo.Object);
        var command = new ResendConfirmationEmail.Command(email, "CZ");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}