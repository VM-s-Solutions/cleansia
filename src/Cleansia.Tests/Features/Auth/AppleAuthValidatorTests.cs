using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// The Apple analogue of <see cref="GoogleAuthValidatorTests"/>. Apple identity-token
/// verification + the nonce binding + the AuthenticationType account-safety guard all live in the
/// verifier/handler against the VERIFIED claims, so the validator enforces SHAPE rules ONLY on the
/// fields the handler actually consumes: the identity token (NotEmpty), the raw nonce (NotEmpty), and
/// the first-login display name. No client-supplied identity field is validated here.
/// </summary>
public class AppleAuthValidatorTests
{
    private readonly AppleAuth.Validator _validator = new();

    private static AppleAuth.Command Cmd(
        string? identityToken = "token",
        string? rawNonce = "nonce",
        string firstName = "First",
        string lastName = "Last") =>
        new(IdentityToken: identityToken!, RawNonce: rawNonce!, FirstName: firstName, LastName: lastName);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task When_IdentityToken_Is_Empty_Then_Validation_Fails_With_Required_Error(string? identityToken)
    {
        var result = await _validator.ValidateAsync(Cmd(identityToken: identityToken));

        Assert.False(result.IsValid);
        var error = result.Errors.Single(e => e.PropertyName == "IdentityToken");
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal("IdentityToken", error.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task When_RawNonce_Is_Empty_Then_Validation_Fails_With_Required_Error(string? rawNonce)
    {
        var result = await _validator.ValidateAsync(Cmd(rawNonce: rawNonce));

        Assert.False(result.IsValid);
        var error = result.Errors.Single(e => e.PropertyName == "RawNonce");
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal("RawNonce", error.ErrorCode);
    }

    [Fact]
    public async Task When_FirstName_Is_Empty_Then_Validation_Fails()
    {
        var result = await _validator.ValidateAsync(Cmd(firstName: ""));

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors.Where(e => e.PropertyName == "FirstName"));
    }

    [Fact]
    public async Task When_LastName_Is_Empty_Then_Validation_Fails()
    {
        var result = await _validator.ValidateAsync(Cmd(lastName: ""));

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors.Where(e => e.PropertyName == "LastName"));
    }

    [Fact]
    public async Task When_All_Required_Shape_Fields_Are_Valid_Then_Validation_Passes()
    {
        var result = await _validator.ValidateAsync(Cmd());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
