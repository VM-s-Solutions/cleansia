using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Owner ruling 2026-09-15: the partner hosts stop provisioning customers. <c>GoogleAuth</c> is routed on
/// both partner hosts, and its provisioning branch brought a Customer into existence there and minted it
/// a partner-audience token — the same defect as the partner <c>Register</c> route, one step further.
/// The handler now reads the host: on a customer host it stays sign-in-or-register; on any other host
/// it signs in an existing Employee or Administrator only (<c>PartnerLogin</c>'s rule), refuses a
/// Customer account with <see cref="BusinessErrorMessage.InsufficientPrivileges"/>, and refuses to
/// provision with <see cref="BusinessErrorMessage.SocialAccountNotFound"/> — the key the sign-in
/// screens already map — whatever the terms tick says.
/// </summary>
public sealed class GoogleAuthHostAudienceGateTests
{
    private const string Token = "verified-token";
    private const string Subject = "google-subject";

    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IGoogleTokenVerifier> _verifier = new();

    public GoogleAuthHostAudienceGateTests()
    {
        _tokenService
            .Setup(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));
    }

    private GoogleAuth.Handler Handler(string hostAudience) => new(
        _verifier.Object,
        _tokenService.Object,
        _userRepository.Object,
        new HostAudienceProvider(hostAudience),
        new Mock<IConsentService>().Object,
        new Mock<ILegalDocumentResolver>().Object,
        new AuditContext(),
        Mock.Of<ICompanySignInGate>());

    private static GoogleAuth.Command Command(string email) =>
        new(Token, GoogleId: "ignored", email, "First", "Last", TermsAccepted: true);

    private User ExistingGoogleAccount(UserProfile profile) => ExistingAccount(profile, AuthenticationType.Google);

    private User ExistingAccount(UserProfile profile, AuthenticationType authenticationType)
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            Profile = profile,
            AuthenticationType = authenticationType,
            GoogleId = authenticationType == AuthenticationType.Google ? Subject : null,
        });
        user.IsActive = true;
        _verifier.Setup(v => v.VerifyAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims(Subject, user.Email, EmailVerified: true));
        _userRepository.Setup(r => r.GetByGoogleIdIgnoringTenantAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.GoogleId is null ? null : user);
        _userRepository.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return user;
    }

    private void NewVerifiedIdentity(string email)
    {
        _verifier.Setup(v => v.VerifyAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims(Subject, email, EmailVerified: true));
        _userRepository.Setup(r => r.GetByGoogleIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository.Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
    }

    private void AssertNothingProvisionedAndNoToken()
    {
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _userRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(JwtAudiences.Partner, UserProfile.Employee)]
    [InlineData(JwtAudiences.Partner, UserProfile.Administrator)]
    [InlineData(JwtAudiences.Mobile, UserProfile.Employee)]
    [InlineData(JwtAudiences.Mobile, UserProfile.Administrator)]
    public async Task A_Partner_Host_Signs_In_An_Existing_Employee_Or_Administrator(string hostAudience, UserProfile profile)
    {
        var user = ExistingGoogleAccount(profile);

        var result = await Handler(hostAudience).Handle(Command(user.Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _tokenService.Verify(t => t.GenerateTokenAsync(user, true, hostAudience, It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
    }

    // The host's verdict comes before the provider's: a password-typed Customer is told the partner host
    // is not its place, not sent to a password sign-in the same host would refuse.
    [Theory]
    [InlineData(JwtAudiences.Partner, AuthenticationType.Google)]
    [InlineData(JwtAudiences.Mobile, AuthenticationType.Google)]
    [InlineData(JwtAudiences.Partner, AuthenticationType.Internal)]
    [InlineData(JwtAudiences.Mobile, AuthenticationType.Internal)]
    public async Task A_Partner_Host_Refuses_A_Customer_Account_As_PartnerLogin_Does(string hostAudience, AuthenticationType authenticationType)
    {
        var customer = ExistingAccount(UserProfile.Customer, authenticationType);

        var result = await Handler(hostAudience).Handle(Command(customer.Email), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InsufficientPrivileges, result.Error!.Message);
        Assert.Equal(nameof(GoogleAuth.Command.Email), result.Error.Code);
        AssertNothingProvisionedAndNoToken();
    }

    [Theory]
    [InlineData(JwtAudiences.Partner)]
    [InlineData(JwtAudiences.Mobile)]
    public async Task A_Partner_Host_Provisions_Nothing_For_A_New_Identity_Even_With_The_Terms_Tick(string hostAudience)
    {
        NewVerifiedIdentity("new-cleaner@example.com");

        var result = await Handler(hostAudience).Handle(Command("new-cleaner@example.com"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.SocialAccountNotFound, result.Error!.Message);
        Assert.Equal(nameof(GoogleAuth.Command.Email), result.Error.Code);
        AssertNothingProvisionedAndNoToken();
    }

    // The gate reads the host, not the marker: the customer hosts keep both branches.
    [Fact]
    public async Task The_Customer_Host_Still_Provisions_A_New_Identity()
    {
        NewVerifiedIdentity("new-customer@example.com");

        var result = await Handler(JwtAudiences.Customer).Handle(Command("new-customer@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.Email == "new-customer@example.com" && u.Profile == UserProfile.Customer)), Times.Once);
    }

    [Fact]
    public async Task The_Customer_Host_Still_Signs_In_A_Customer()
    {
        var customer = ExistingGoogleAccount(UserProfile.Customer);

        var result = await Handler(JwtAudiences.Customer).Handle(Command(customer.Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _tokenService.Verify(t => t.GenerateTokenAsync(customer, true, JwtAudiences.Customer, It.IsAny<CancellationToken>()), Times.Once);
    }
}
