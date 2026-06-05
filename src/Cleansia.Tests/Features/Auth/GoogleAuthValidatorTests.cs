using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// T-0105 (IDA-SEC-01): Google ID-token verification now lives in <c>IGoogleTokenVerifier</c> (called
/// by the handler), so the validator only enforces SHAPE rules and the auth-type guard — it no longer
/// takes <c>IGoogleConfig</c> and no longer has any <c>IsDevelopment</c> bypass. AC7 reconciles the two
/// tests that previously depended on that bypass; the required-field / email-format /
/// <see cref="BusinessErrorMessage.InternalAuthTypeError"/> cases stay green.
/// </summary>
public class GoogleAuthValidatorTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GoogleAuth.Validator _validator;

    public GoogleAuthValidatorTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _validator = new GoogleAuth.Validator(_userRepositoryMock.Object);
    }

    #region Token Validation Tests

    [Fact]
    public async Task When_Token_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        var command = new GoogleAuth.Command(null, "googleId", "email@example.com", "First", "Last");
        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        var error = result.Errors.Single(e => e.PropertyName == "Token");
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal("Token", error.ErrorCode);
    }

    [Fact]
    public async Task When_Token_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        var command = new GoogleAuth.Command("", "googleId", "email@example.com", "First", "Last");
        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        var error = result.Errors.Single(e => e.PropertyName == "Token");
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal("Token", error.ErrorCode);
    }

    // AC7 — token verification has LEFT the validator (it now lives in IGoogleTokenVerifier, called by
    // the handler). The validator's only Token rule is NotEmpty, so a non-empty token produces no Token
    // error here regardless of any (now-deleted) IsDevelopment flag.
    [Fact]
    public async Task When_Token_Is_Not_Empty_Then_No_Token_Validation_Error()
    {
        var command = new GoogleAuth.Command("someToken", "googleId", "email@example.com", "First", "Last");
        _userRepositoryMock.Setup(r => r.GetByEmailAsync("email@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User)null);

        var result = await _validator.ValidateAsync(command);

        var tokenErrors = result.Errors.Where(e => e.PropertyName == "Token").ToList();
        Assert.Empty(tokenErrors);
    }

    #endregion

    #region GoogleId Validation Tests

    [Fact]
    public async Task When_GoogleId_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        var command = new GoogleAuth.Command("token", null, "email@example.com", "First", "Last");
        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        var error = result.Errors.First(e => e.PropertyName == "GoogleId");
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal("GoogleId", error.ErrorCode);
    }

    [Fact]
    public async Task When_GoogleId_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        var command = new GoogleAuth.Command("token", "", "email@example.com", "First", "Last");
        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        var error = result.Errors.First(e => e.PropertyName == "GoogleId");
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal("GoogleId", error.ErrorCode);
    }

    #endregion

    #region Email Validation Tests

    [Fact]
    public async Task When_Email_Is_Associated_With_Non_Google_User_Then_Validation_Fails()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { AuthenticationType = AuthenticationType.Internal });
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var command = new GoogleAuth.Command("token", "googleId", user.Email, "First", "Last");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        var error = result.Errors.First(e => e.PropertyName == "Email");
        Assert.Equal(BusinessErrorMessage.InternalAuthTypeError, error.ErrorMessage);
        Assert.Equal("Email", error.ErrorCode);
    }

    [Fact]
    public async Task When_Email_Is_Not_Associated_With_Any_User_Then_Validation_Passes()
    {
        var email = "email@example.com";
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var command = new GoogleAuth.Command("token", "googleId", email, "First", "Last");

        var result = await _validator.ValidateAsync(command);

        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Empty(emailErrors);
    }

    [Fact]
    public async Task When_Email_Is_Associated_With_Google_User_Then_Validation_Passes()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { AuthenticationType = AuthenticationType.Google });
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var command = new GoogleAuth.Command("token", "googleId", user.Email, "First", "Last");

        var result = await _validator.ValidateAsync(command);

        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.Empty(emailErrors);
    }

    [Fact]
    public async Task When_Email_Is_Invalid_Then_Validation_Fails()
    {
        var command = new GoogleAuth.Command("token", "googleId", "invalid_email", "First", "Last");
        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        var emailErrors = result.Errors.Where(e => e.PropertyName == "Email").ToList();
        Assert.NotEmpty(emailErrors);
    }

    #endregion

    #region Inherited Rules Tests

    [Fact]
    public async Task When_FirstName_Is_Empty_Then_Validation_Fails()
    {
        var command = new GoogleAuth.Command("token", "googleId", "email@example.com", "", "Last");
        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        var firstNameErrors = result.Errors.Where(e => e.PropertyName == "FirstName").ToList();
        Assert.NotEmpty(firstNameErrors);
    }

    [Fact]
    public async Task When_LastName_Is_Empty_Then_Validation_Fails()
    {
        var command = new GoogleAuth.Command("token", "googleId", "email@example.com", "First", "");
        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        var lastNameErrors = result.Errors.Where(e => e.PropertyName == "LastName").ToList();
        Assert.NotEmpty(lastNameErrors);
    }

    #endregion

    #region Valid Case Test

    [Fact]
    public async Task When_All_Fields_Are_Valid_Then_Validation_Passes()
    {
        var email = "email@example.com";
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        var command = new GoogleAuth.Command("token", "googleId", email, "First", "Last");

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion
}
