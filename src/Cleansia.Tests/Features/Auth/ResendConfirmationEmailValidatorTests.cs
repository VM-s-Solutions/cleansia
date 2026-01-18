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
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("cs", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new ResendConfirmationEmail.Validator(mockUserRepo.Object, mockLangRepo.Object);
        var command = new ResendConfirmationEmail.Command(null, "cs");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Single(emailErrors);
        Assert.Equal(BusinessErrorMessage.Required, emailErrors[0].ErrorMessage);
        Assert.Equal("Email", emailErrors[0].ErrorCode);
    }

    [Fact]
    public async Task When_Email_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockUserRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("cs", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new ResendConfirmationEmail.Validator(mockUserRepo.Object, mockLangRepo.Object);
        var command = new ResendConfirmationEmail.Command("", "cs");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Single(emailErrors);
        Assert.Equal(BusinessErrorMessage.Required, emailErrors[0].ErrorMessage);
        Assert.Equal("Email", emailErrors[0].ErrorCode);
    }

    [Fact]
    public async Task When_Email_Does_Not_Exist_Then_Validation_Fails_With_NotExistingUser_Error()
    {
        // Arrange
        var email = "test@example.com";
        var mockUserRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockUserRepo.Setup(r => r.ExistsWithEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("cs", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new ResendConfirmationEmail.Validator(mockUserRepo.Object, mockLangRepo.Object);
        var command = new ResendConfirmationEmail.Command(email, "cs");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Single(emailErrors);
        Assert.Equal(BusinessErrorMessage.NotExistingUserWithEmail, emailErrors[0].ErrorMessage);
        Assert.Equal("Email", emailErrors[0].ErrorCode);
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
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("cs", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new ResendConfirmationEmail.Validator(mockUserRepo.Object, mockLangRepo.Object);
        var command = new ResendConfirmationEmail.Command(email, "cs");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Single(emailErrors);
        Assert.Equal(BusinessErrorMessage.EmailConfirmed, emailErrors[0].ErrorMessage);
        Assert.Equal("Email", emailErrors[0].ErrorCode);
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
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("cs", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new ResendConfirmationEmail.Validator(mockUserRepo.Object, mockLangRepo.Object);
        var command = new ResendConfirmationEmail.Command(email, "cs");

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}