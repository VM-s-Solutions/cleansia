using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Internalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Auth;

public class RegisterValidatorTests
{
    #region Email Validation Tests
    [Fact]
    public async Task When_Email_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command(null, "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
        Assert.Equal("Email", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_Email_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync("", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("", "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
        Assert.Equal("Email", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_Email_Is_Invalid_Format_Then_Validation_Fails_With_InvalidFormat_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("invalid", "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.InvalidEmailFormat, errors[0].ErrorMessage);
        Assert.Equal("Email", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_Email_Is_Longer_Than_50_Characters_Then_Validation_Fails_With_MaxLength_Error()
    {
        // Arrange
        var longEmail = new string('a', 40) + "@example.com"; // 52 characters
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command(longEmail, "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.MaxLength, errors[0].ErrorMessage);
        Assert.Equal("Email", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_Email_Is_Associated_With_Confirmed_User_Then_Validation_Fails()
    {
        // Arrange
        var user = UserMockFactory.Generate();
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command(user.Email, "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.ExistingUserWithEmail, errors[0].ErrorMessage);
        Assert.Equal("Email", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_Email_Is_Associated_With_Unconfirmed_User_Then_Validation_Passes_For_Email()
    {
        // Arrange
        var user = User.CreateWithPassword(
            TestUtilities.Constants.TestUserSession.TestUserEmail,
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            TestUtilities.Constants.TestUserSession.TestFirstName,
            TestUtilities.Constants.TestUserSession.TestLastName);
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command(user.Email, "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Empty(emailErrors);
    }
    [Fact]
    public async Task When_Email_Does_Not_Exist_Then_Validation_Passes_For_Email()
    {
        // Arrange
        var email = "new@example.com";
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command(email, "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Empty(emailErrors);
    }
    #endregion
    #region FirstName Validation Tests
    [Fact]
    public async Task When_FirstName_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", null, "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "FirstName").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
        Assert.Equal("FirstName", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_FirstName_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "FirstName").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
        Assert.Equal("FirstName", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_FirstName_Is_Longer_Than_50_Characters_Then_Validation_Fails_With_MaxLength_Error()
    {
        // Arrange
        var longFirstName = new string('a', 51);
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", longFirstName, "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "FirstName").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.MaxLength, errors[0].ErrorMessage);
        Assert.Equal("FirstName", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_FirstName_Is_Valid_Then_Validation_Passes_For_FirstName()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        var firstNameErrors = result.Errors.Where(e => e.PropertyName == "FirstName").ToList();
        Assert.Empty(firstNameErrors);
    }
    #endregion
    #region LastName Validation Tests
    [Fact]
    public async Task When_LastName_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "John", null, "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "LastName").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
        Assert.Equal("LastName", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_LastName_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "John", "", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "LastName").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
        Assert.Equal("LastName", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_LastName_Is_Longer_Than_50_Characters_Then_Validation_Fails_With_MaxLength_Error()
    {
        // Arrange
        var longLastName = new string('a', 51);
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "John", longLastName, "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "LastName").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.MaxLength, errors[0].ErrorMessage);
        Assert.Equal("LastName", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_LastName_Is_Valid_Then_Validation_Passes_For_LastName()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        var lastNameErrors = result.Errors.Where(e => e.PropertyName == "LastName").ToList();
        Assert.Empty(lastNameErrors);
    }
    #endregion
    #region Password Validation Tests
    [Fact]
    public async Task When_Password_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", null, "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Password").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
        Assert.Equal("Password", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_Password_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Password").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
        Assert.Equal("Password", errors[0].ErrorCode);
    }
    [Theory]
    [InlineData("password1", "Missing uppercase letter")]
    [InlineData("PASSWORD1", "Missing lowercase letter")]
    [InlineData("Password", "Missing digit")]
    [InlineData("Pass1", "Less than 8 characters")]
    public async Task When_Password_Does_Not_Match_Pattern_Then_Validation_Fails_With_InvalidFormat_Error(string password, string reason)
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", password, "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Password").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.InvalidPasswordFormat, errors[0].ErrorMessage);
        Assert.Equal("Password", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_Password_Matches_Pattern_Then_Validation_Passes_For_Password()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        var passwordErrors = result.Errors.Where(e => e.PropertyName == "Password").ToList();
        Assert.Empty(passwordErrors);
    }
    #endregion
    #region Language Validation Tests
    [Fact]
    public async Task When_Language_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "John", "Doe", null);
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Language").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
        Assert.Equal("Language", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_Language_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "John", "Doe", "");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Language").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
        Assert.Equal("Language", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_Language_Does_Not_Exist_Then_Validation_Fails_With_InvalidEnumValue_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("invalid", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "John", "Doe", "invalid");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Language").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.InvalidEnumValue, errors[0].ErrorMessage);
        Assert.Equal("Language", errors[0].ErrorCode);
    }
    [Fact]
    public async Task When_Language_Exists_Then_Validation_Passes_For_Language()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object);
        var command = new Register.Command("test@example.com", "Password1!", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        var languageErrors = result.Errors.Where(e => e.PropertyName == "Language").ToList();
        Assert.Empty(languageErrors);
    }
    #endregion
}