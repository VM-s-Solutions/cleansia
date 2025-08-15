using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Auth;

public class LoginValidatorTests
{
    private readonly Mock<IUserRepository> _mockRepo;
    private readonly Login.Validator _validator;

    public LoginValidatorTests()
    {
        _mockRepo = new Mock<IUserRepository>();
        _validator = new Login.Validator(_mockRepo.Object);
    }

    [Fact]
    public async Task When_Email_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var command = new Login.Command(null, "password", true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.NotEmpty(emailErrors);
        Assert.Contains(emailErrors, e => e.ErrorMessage == BusinessErrorMessage.Required && e.ErrorCode == "Email");
        Assert.Empty(result.Errors.Where(e => e.PropertyName == "Password"));
    }

    [Fact]
    public async Task When_Email_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var command = new Login.Command("", "password", true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.NotEmpty(emailErrors);
        Assert.Contains(emailErrors, e => e.ErrorMessage == BusinessErrorMessage.Required && e.ErrorCode == "Email");
        Assert.Empty(result.Errors.Where(e => e.PropertyName == "Password"));
    }

    [Fact]
    public async Task When_Email_Is_Invalid_Format_Then_Validation_Fails()
    {
        // Arrange
        var command = new Login.Command("invalid_email", "password", true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.NotEmpty(emailErrors);
        // Assuming AddEmailRules includes EmailAddress() validation
        Assert.Contains(emailErrors, e => e.ErrorMessage.Contains("email"));
        Assert.Empty(result.Errors.Where(e => e.PropertyName == "Password"));
    }

    [Fact]
    public async Task When_Email_Does_Not_Exist_Then_Validation_Fails_With_NotExistingUser_Error()
    {
        // Arrange
        var email = "nonexistent@example.com";
        _mockRepo.Setup(r => r.ExistsWithEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockRepo.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var command = new Login.Command(email, "password", true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Contains(emailErrors, e => e.ErrorMessage == BusinessErrorMessage.NotExistingUserWithEmail && e.ErrorCode == "Email");
        Assert.Empty(result.Errors.Where(e => e.PropertyName == "Password"));
    }

    [Fact]
    public async Task When_Email_Exists_But_Is_Google_Auth_Then_Validation_Fails_With_GoogleAuthTypeError()
    {
        // Arrange
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { AuthenticationType = AuthenticationType.Google });
        _mockRepo.Setup(r => r.ExistsWithEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var command = new Login.Command(user.Email, "password", true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Contains(emailErrors, e => e.ErrorMessage == BusinessErrorMessage.GoogleAuthTypeError && e.ErrorCode == "Email");
        Assert.Empty(result.Errors.Where(e => e.PropertyName == "Password"));
    }

    [Fact]
    public async Task When_Email_Exists_And_Is_Internal_But_Password_Is_Empty_Then_Validation_Fails()
    {
        // Arrange
        var user = UserMockFactory.Generate();
        _mockRepo.Setup(r => r.ExistsWithEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var command = new Login.Command(user.Email, string.Empty, true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var passwordErrors = result.Errors.Where(e => e.PropertyName == "Password").ToList();
        Assert.Contains(passwordErrors, e => e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task When_Email_Exists_And_Is_Internal_But_Password_Is_Incorrect_Then_Validation_Fails()
    {
        // Arrange
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Password = TestUtilities.Constants.TestUserSession.TestUserPassword.HashAndSaltPassword() });
        _mockRepo.Setup(r => r.ExistsWithEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var command = new Login.Command(user.Email, TestUtilities.Constants.TestUserSession.TestUserPassword + "s", true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var passwordErrors = result.Errors.Where(e => e.PropertyName == "Password").ToList();
        Assert.Contains(passwordErrors, e => e.ErrorMessage == BusinessErrorMessage.InvalidPassword);
    }

    [Fact]
    public async Task When_All_Fields_Are_Valid_Then_Validation_Passes()
    {
        // Arrange
        const string password = TestUtilities.Constants.TestUserSession.TestUserPassword;
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Password = password.HashAndSaltPassword() });
        _mockRepo.Setup(r => r.ExistsWithEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var command = new Login.Command(user.Email, password, true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}