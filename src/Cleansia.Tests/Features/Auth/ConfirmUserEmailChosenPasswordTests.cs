using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Account pre-hijacking, closed at the confirmation (owner ruling 2026-10-26, "Close"): whoever confirms an
/// address proves they hold its mailbox, so the account ends with the password they chose there, under the
/// password rules every registration applies, and with no password any registration stored. A confirmation
/// that brings none, from a client built before the confirm screens asked for one, leaves no password.
/// </summary>
public sealed class ConfirmUserEmailChosenPasswordTests
{
    private const string StrangersPassword = "stranger-Passw0rd";
    private const string OwnersPassword = "owner-Passw0rd";

    private readonly Mock<IUserRepository> _userRepository = new();

    public ConfirmUserEmailChosenPasswordTests()
    {
        _userRepository
            .Setup(r => r.TryChargeConfirmationCodeAttemptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Theory]
    [InlineData(UserProfile.Customer, JwtAudiences.Customer)]
    [InlineData(UserProfile.Employee, JwtAudiences.Partner)]
    [InlineData(UserProfile.Employee, JwtAudiences.Mobile)]
    public async Task The_Confirmation_Leaves_The_Password_Its_Confirmer_Chose_And_None_A_Registration_Stored(
        UserProfile profile, string hostAudience)
    {
        var (user, otp) = RegisteredBy(StrangersPassword, profile);

        var result = await Handler(hostAudience).Handle(
            new ConfirmUserEmail.Command(otp, user.Email, OwnersPassword), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(user.IsEmailConfirmed);
        Assert.Equal(OwnersPassword, user.Password);
    }

    [Fact]
    public async Task A_Confirmation_That_Brings_No_Password_Leaves_The_Account_With_None()
    {
        var (user, otp) = RegisteredBy(StrangersPassword, UserProfile.Customer);

        var result = await Handler(JwtAudiences.Customer).Handle(
            new ConfirmUserEmail.Command(otp, user.Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(user.IsEmailConfirmed);
        Assert.Null(user.Password);
    }

    [Theory]
    [InlineData(JwtAudiences.Partner)]
    [InlineData(JwtAudiences.Mobile)]
    public async Task A_Customers_Code_Refused_On_A_Partner_Host_Leaves_The_Stored_Password_Alone(string hostAudience)
    {
        var (user, otp) = RegisteredBy(StrangersPassword, UserProfile.Customer);

        var result = await Handler(hostAudience).Handle(
            new ConfirmUserEmail.Command(otp, user.Email, OwnersPassword), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.True(StrangersPassword.VerifyPassword(user.Password!));
    }

    [Theory]
    [InlineData("passw0r", BusinessErrorMessage.InvalidPasswordFormat)]
    [InlineData("password", BusinessErrorMessage.InvalidPasswordFormat)]
    [InlineData("12345678", BusinessErrorMessage.InvalidPasswordFormat)]
    [InlineData("", BusinessErrorMessage.Required)]
    public async Task A_Chosen_Password_The_Password_Rules_Refuse_Is_Refused_On_The_Password(string password, string error)
    {
        var (user, otp) = RegisteredBy(StrangersPassword, UserProfile.Customer);

        var result = await Validator().ValidateAsync(new ConfirmUserEmail.Command(otp, user.Email, password));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(error, failure.ErrorMessage);
        Assert.Equal(nameof(ConfirmUserEmail.Command.Password), failure.ErrorCode);
    }

    [Theory]
    [InlineData(OwnersPassword)]
    [InlineData("Пароль2024")]
    [InlineData(null)]
    public async Task A_Chosen_Password_The_Rules_Accept_Or_None_At_All_Passes(string? password)
    {
        var (user, otp) = RegisteredBy(StrangersPassword, UserProfile.Customer);

        var result = await Validator().ValidateAsync(new ConfirmUserEmail.Command(otp, user.Email, password));

        Assert.True(result.IsValid);
    }

    private ConfirmUserEmail.Validator Validator() =>
        new(_userRepository.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>(), new AuditContext());

    private ConfirmUserEmail.Handler Handler(string hostAudience)
    {
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));
        return new ConfirmUserEmail.Handler(
            tokenService.Object, _userRepository.Object, new HostAudienceProvider(hostAudience), new AuditContext());
    }

    private (User User, string Otp) RegisteredBy(string password, UserProfile profile)
    {
        var user = User.CreateWithPassword("owner@cleansia.test", password.HashAndSaltPassword(), "Jana", "Nováková", profile);
        user.Created(user.Email, DateTime.UtcNow);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return (user, user.RawConfirmationToken!);
    }
}
