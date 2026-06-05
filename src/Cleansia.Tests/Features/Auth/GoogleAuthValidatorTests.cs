using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Google ID-token verification lives in
/// <c>IGoogleTokenVerifier</c> (called by the handler) and the AuthenticationType account-safety guard
/// now runs IN THE HANDLER against the VERIFIED <c>claims.Email</c> (see
/// <c>GoogleAuthHandlerTests.Existing_Internal_Account_With_Verified_Email_Is_Rejected...</c>).
///
/// The validator therefore enforces SHAPE rules ONLY, on the fields the handler actually consumes: the
/// token (NotEmpty) and the display name. It no longer takes <c>IUserRepository</c> and no longer
/// validates <c>command.Email</c> / <c>command.GoogleId</c> — those are client-supplied and the handler
/// ignores them, so validating them gave a false sense of a guard on the wrong, unverified email.
/// </summary>
public class GoogleAuthValidatorTests
{
    private readonly GoogleAuth.Validator _validator = new();

    private static GoogleAuth.Command Cmd(string? token, string firstName = "First", string lastName = "Last") =>
        // GoogleId/Email are no longer validated; pass arbitrary values to prove they don't affect the result.
        new(Token: token!, GoogleId: "ignored", Email: "ignored", FirstName: firstName, LastName: lastName);

    [Fact]
    public async Task When_Token_Is_Null_Then_Validation_Fails_With_Required_Error()
    {
        var result = await _validator.ValidateAsync(Cmd(null));

        Assert.False(result.IsValid);
        var error = result.Errors.Single(e => e.PropertyName == "Token");
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal("Token", error.ErrorCode);
    }

    [Fact]
    public async Task When_Token_Is_Empty_Then_Validation_Fails_With_Required_Error()
    {
        var result = await _validator.ValidateAsync(Cmd(""));

        Assert.False(result.IsValid);
        var error = result.Errors.Single(e => e.PropertyName == "Token");
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal("Token", error.ErrorCode);
    }

    [Fact]
    public async Task When_Token_Is_Not_Empty_Then_No_Token_Validation_Error()
    {
        var result = await _validator.ValidateAsync(Cmd("someToken"));

        Assert.Empty(result.Errors.Where(e => e.PropertyName == "Token"));
    }

    [Fact]
    public async Task Email_And_GoogleId_Are_Not_Validated_By_The_Validator()
    {
        // Garbage email + empty GoogleId no longer produce validation errors — identity is the handler's
        // job against the verified token.
        var result = await _validator.ValidateAsync(
            new GoogleAuth.Command(Token: "token", GoogleId: "", Email: "not-an-email", FirstName: "First", LastName: "Last"));

        Assert.Empty(result.Errors.Where(e => e.PropertyName == "Email"));
        Assert.Empty(result.Errors.Where(e => e.PropertyName == "GoogleId"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task When_FirstName_Is_Empty_Then_Validation_Fails()
    {
        var result = await _validator.ValidateAsync(Cmd("token", firstName: ""));

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors.Where(e => e.PropertyName == "FirstName"));
    }

    [Fact]
    public async Task When_LastName_Is_Empty_Then_Validation_Fails()
    {
        var result = await _validator.ValidateAsync(Cmd("token", lastName: ""));

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors.Where(e => e.PropertyName == "LastName"));
    }

    [Fact]
    public async Task When_All_Required_Shape_Fields_Are_Valid_Then_Validation_Passes()
    {
        var result = await _validator.ValidateAsync(Cmd("token"));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
