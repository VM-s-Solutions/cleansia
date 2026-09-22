using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
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
    private readonly AuditContext _auditContext = new();
    private readonly Login.Validator _validator;

    public LoginValidatorTests()
    {
        _mockRepo = new Mock<IUserRepository>();
        _validator = new Login.Validator(_mockRepo.Object, Mock.Of<IRefreshTokenRepository>(), Mock.Of<IRefreshTokenService>(), _auditContext);
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
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == "Password");
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
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == "Password");
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
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task When_Email_Does_Not_Exist_Then_Validation_Fails_With_NotExistingUser_Error()
    {
        // Arrange
        var email = "nonexistent@example.com";
        _mockRepo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var command = new Login.Command(email, "password", true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Contains(emailErrors, e => e.ErrorMessage == BusinessErrorMessage.NotExistingUserWithEmail && e.ErrorCode == "Email");
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task When_Email_Exists_But_Is_Google_Auth_Then_Validation_Fails_With_GoogleAuthTypeError()
    {
        // Arrange
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { AuthenticationType = AuthenticationType.Google });
        _mockRepo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var command = new Login.Command(user.Email, "password", true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Contains(emailErrors, e => e.ErrorMessage == BusinessErrorMessage.GoogleAuthTypeError && e.ErrorCode == "Email");
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task When_Email_Exists_But_Is_Apple_Auth_Then_Validation_Fails_With_AppleAuthTypeError()
    {
        // Arrange
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { AuthenticationType = AuthenticationType.Apple });
        _mockRepo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var command = new Login.Command(user.Email, "password", true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Contains(emailErrors, e => e.ErrorMessage == BusinessErrorMessage.AppleAuthTypeError && e.ErrorCode == "Email");
        // The bug this test guards: an Apple account used to be told it signed in with Google.
        Assert.DoesNotContain(emailErrors, e => e.ErrorMessage == BusinessErrorMessage.GoogleAuthTypeError);
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task When_Email_Exists_But_Auth_Type_Is_Unknown_Then_Validation_Fails_With_Neutral_ExternalAuthTypeError()
    {
        // Arrange — an AuthenticationType value the validator has no arm for stands in for a provider
        // added after this test was written. It must be refused, and refused WITHOUT claiming Google or
        // Apple: that silent inheritance is exactly what the hardcoded message used to do.
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { AuthenticationType = (AuthenticationType)99 });
        _mockRepo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var command = new Login.Command(user.Email, "password", true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Contains(emailErrors, e => e.ErrorMessage == BusinessErrorMessage.ExternalAuthTypeError && e.ErrorCode == "Email");
        Assert.DoesNotContain(emailErrors, e => e.ErrorMessage == BusinessErrorMessage.GoogleAuthTypeError || e.ErrorMessage == BusinessErrorMessage.AppleAuthTypeError);
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task When_Email_Exists_And_Is_Internal_But_Password_Is_Empty_Then_Validation_Fails()
    {
        // Arrange
        var user = UserMockFactory.Generate();
        _mockRepo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
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
        _mockRepo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
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
        _mockRepo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var command = new Login.Command(user.Email, password, true);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ── the refused account is named to the audit context ──────────────────────

    /// <summary>
    /// A refusal on a KNOWN account is that account's audit row: the validator is the only step that
    /// resolves the typed address before refusing, so it names the subject (id only, no payload) through
    /// the seam the handlers use — nothing of it reaches the caller.
    /// </summary>
    [Fact]
    public async Task A_Wrong_Password_On_A_Known_Account_Names_That_Account_With_No_Payload()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Password = TestUtilities.Constants.TestUserSession.TestUserPassword.HashAndSaltPassword() });
        _mockRepo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _validator.ValidateAsync(new Login.Command(user.Email, TestUtilities.Constants.TestUserSession.TestUserPassword + "s", true));

        Assert.False(result.IsValid);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal(user.Id, snapshot!.ActorUserId);
        Assert.Equal("User", snapshot.ResourceType);
        Assert.Equal(user.Id, snapshot.ResourceId);
        Assert.Null(snapshot.AfterJson);
    }

    [Fact]
    public async Task A_Google_Account_Refused_The_Password_Path_Is_Named_Too()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { AuthenticationType = AuthenticationType.Google });
        _mockRepo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _validator.ValidateAsync(new Login.Command(user.Email, "password", true));

        Assert.False(result.IsValid);
        Assert.Equal(user.Id, _auditContext.DrainSnapshot()?.ActorUserId);
    }

    /// <summary>An unknown address resolves nothing, so nothing is named — the row stays the key, the IP and the device.</summary>
    [Fact]
    public async Task An_Unknown_Address_Names_Nobody()
    {
        const string email = "nonexistent@example.com";
        _mockRepo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User)null);

        var result = await _validator.ValidateAsync(new Login.Command(email, "password", true));

        Assert.False(result.IsValid);
        Assert.Null(_auditContext.DrainSnapshot());
    }
}