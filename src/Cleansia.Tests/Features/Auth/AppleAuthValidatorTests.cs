using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// The Apple analogue of <see cref="GoogleAuthValidatorTests"/>. Apple identity-token
/// verification + the nonce binding + the AuthenticationType account-safety guard all live in the
/// verifier/handler against the VERIFIED claims, so the validator enforces SHAPE rules ONLY on the
/// fields the handler actually consumes: the identity token (NotEmpty), the raw nonce (NotEmpty), and a
/// length cap on the OPTIONAL display name. Apple returns a name only on the first authorization, may
/// omit the family name, and sends no name on later sign-ins, so the name is never required. No
/// client-supplied identity field is validated here.
/// </summary>
public class AppleAuthValidatorTests
{
    private readonly AppleAuth.Validator _validator = new();

    private static AppleAuth.Command Cmd(
        string? identityToken = "token",
        string? rawNonce = "nonce",
        string? firstName = "First",
        string? lastName = "Last") =>
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

    // Apple returns a name only on the first authorization; the family name is often absent, so an empty
    // or missing last name is a valid Apple request (regression: the backend used to reject it with
    // "last name is required").
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task When_LastName_Is_Missing_Or_Empty_Then_Validation_Passes(string? lastName)
    {
        var result = await _validator.ValidateAsync(Cmd(lastName: lastName));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors.Where(e => e.PropertyName == "LastName"));
    }

    // Later Apple sign-ins carry no name at all — both fields absent must still validate (identity is
    // bound from the verified token, not the name).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task When_FirstName_Is_Missing_Or_Empty_Then_Validation_Passes(string? firstName)
    {
        var result = await _validator.ValidateAsync(Cmd(firstName: firstName));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors.Where(e => e.PropertyName == "FirstName"));
    }

    [Fact]
    public async Task When_Both_Names_Are_Missing_Then_Validation_Passes()
    {
        var result = await _validator.ValidateAsync(Cmd(firstName: null, lastName: null));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task When_LastName_Exceeds_Max_Length_Then_Validation_Fails_With_MaxLength()
    {
        var result = await _validator.ValidateAsync(Cmd(lastName: new string('a', 51)));

        Assert.False(result.IsValid);
        var error = result.Errors.Single(e => e.PropertyName == "LastName");
        Assert.Equal(BusinessErrorMessage.MaxLength, error.ErrorMessage);
        Assert.Equal("LastName", error.ErrorCode);
    }

    [Fact]
    public async Task When_All_Required_Shape_Fields_Are_Valid_Then_Validation_Passes()
    {
        var result = await _validator.ValidateAsync(Cmd());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
