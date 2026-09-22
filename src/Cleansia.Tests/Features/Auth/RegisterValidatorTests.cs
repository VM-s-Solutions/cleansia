using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Auth;

public class RegisterValidatorTests
{
    #region Email Validation Tests

    private static Register.Validator ValidatorScopedTo(string? ambientTenant, User? existing)
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var tenant = new Mock<ITenantProvider>();
        tenant.Setup(p => p.GetCurrentTenantId()).Returns(ambientTenant);
        return new Register.Validator(mockRepo.Object, mockLangRepo.Object, tenant.Object);
    }

    private static Register.Command Registration() => new("person@example.com", "Password1!@abc", "John", "Doe", "en");

    /// <summary>
    /// One identity per email across the holding (ADR-0061 D5.1): the pre-check reads ignoring the
    /// tenant, so an address a CONFIRMED account in another operating company holds is refused here,
    /// as a 400, before the flush.
    /// </summary>
    [Fact]
    public async Task When_Email_Is_Held_By_A_Confirmed_User_Of_Another_Operator_Then_Validation_Fails_With_ExistingUserWithEmail()
    {
        var existing = User.CreateWithPassword("person@example.com", "Password1!@abc", "Some", "One");
        existing.ConfirmEmail();
        existing.TenantId = "cleansia-sk";

        var result = await ValidatorScopedTo("cleansia-cz", existing).ValidateAsync(Registration());

        var error = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ExistingUserWithEmail);
        Assert.Equal("Email", error.ErrorCode);
    }

    /// <summary>
    /// An UNCONFIRMED row may be re-registered (the handler refreshes its code) only in the market this
    /// request was scoped to — a visitor never silently re-registers into a company they did not choose.
    /// </summary>
    [Fact]
    public async Task When_Email_Is_Held_By_An_Unconfirmed_User_Of_Another_Operator_Then_Validation_Fails_With_ExistingUserWithEmail()
    {
        var existing = User.CreateWithPassword("person@example.com", "Password1!@abc", "Some", "One");
        existing.TenantId = "cleansia-sk";

        var result = await ValidatorScopedTo("cleansia-cz", existing).ValidateAsync(Registration());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ExistingUserWithEmail);
    }

    [Fact]
    public async Task When_Email_Is_Held_By_An_Unconfirmed_User_Of_The_Same_Operator_Then_ReRegistration_Is_Allowed()
    {
        var existing = User.CreateWithPassword("person@example.com", "Password1!@abc", "Some", "One");
        existing.TenantId = "cleansia-cz";

        var result = await ValidatorScopedTo("cleansia-cz", existing).ValidateAsync(Registration());

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ExistingUserWithEmail);
    }

    [Fact]
    public async Task When_Email_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command(null, "Password1!@abc", "John", "Doe", "en");
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
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync("", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("", "Password1!@abc", "John", "Doe", "en");
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
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("invalid", "Password1!@abc", "John", "Doe", "en");
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
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command(longEmail, "Password1!@abc", "John", "Doe", "en");
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
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command(user.Email, "Password1!@abc", "John", "Doe", "en");
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
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command(user.Email, "Password1!@abc", "John", "Doe", "en");
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
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command(email, "Password1!@abc", "John", "Doe", "en");
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
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", null, "Doe", "en");
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
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "", "Doe", "en");
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
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", longFirstName, "Doe", "en");
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
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "John", "Doe", "en");
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
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "John", null, "en");
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
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "John", "", "en");
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
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "John", longLastName, "en");
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
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "John", "Doe", "en");
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
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
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
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
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
    [InlineData("Password", "Missing digit")]
    [InlineData("Pass1", "Less than 8 characters")]
    [InlineData("12345678", "Missing letter")]
    public async Task When_Password_Does_Not_Match_Pattern_Then_Validation_Fails_With_InvalidFormat_Error(string password, string reason)
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", password, "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid, $"Expected '{password}' to be rejected: {reason}");
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
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "John", "Doe", "en");
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
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "John", "Doe", null);
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
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "John", "Doe", "");
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
    public async Task When_Language_Does_Not_Exist_Then_Validation_Fails_With_LanguageNotSupported_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("invalid", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "John", "Doe", "invalid");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == "Language").ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.LanguageNotSupported, errors[0].ErrorMessage);
    }
    [Fact]
    public async Task When_Language_Exists_Then_Validation_Passes_For_Language()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByEmailIgnoringTenantAsync("test@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var mockLangRepo = new Mock<ILanguageRepository>();
        mockLangRepo.Setup(r => r.ExistsWithCodeAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new Register.Validator(mockRepo.Object, mockLangRepo.Object, Mock.Of<ITenantProvider>());
        var command = new Register.Command("test@example.com", "Password1!@abc", "John", "Doe", "en");
        // Act
        var result = await validator.ValidateAsync(command);
        // Assert
        var languageErrors = result.Errors.Where(e => e.PropertyName == "Language").ToList();
        Assert.Empty(languageErrors);
    }
    #endregion
    #region Terms Tick Tests

    // A customer's account comes into existence only on an asserted tick (owner ruling 2026-09-14).
    // Null (a client that sends nothing) and false (an unticked box) are the same refusal — neither is
    // a consent.
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task When_The_Terms_Tick_Is_Not_Asserted_Then_Validation_Fails_With_TermsNotAccepted(bool? termsAccepted)
    {
        var result = await ValidatorScopedTo("cleansia-cz", existing: null)
            .ValidateAsync(Registration() with { TermsAccepted = termsAccepted });

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, e => e.PropertyName == nameof(Register.Command.TermsAccepted));
        Assert.Equal(BusinessErrorMessage.TermsNotAccepted, error.ErrorMessage);
        Assert.Equal(nameof(Register.Command.TermsAccepted), error.ErrorCode);
    }

    [Fact]
    public async Task When_The_Terms_Tick_Is_Asserted_Then_Validation_Passes()
    {
        var result = await ValidatorScopedTo("cleansia-cz", existing: null)
            .ValidateAsync(Registration() with { TermsAccepted = true });

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.ErrorCode}={e.ErrorMessage}")));
    }
    #endregion
}
