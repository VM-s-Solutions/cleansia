using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// <c>ConfirmUserEmail</c> is routed anonymously on both partner hosts and mints a session for whichever
/// account the code proves. With the partner <c>Register</c> route gone, a Customer's code presented there
/// was the last way a partner-audience token could carry a Customer role. The handler reads the host the
/// way <c>GoogleAuth</c> does: off a customer host it confirms an Employee or an Administrator only and
/// refuses a Customer with <see cref="BusinessErrorMessage.InsufficientPrivileges"/> — no confirmation,
/// no session — so the account confirms where it belongs.
/// </summary>
public sealed class ConfirmUserEmailHostAudienceGateTests
{
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IUserRepository> _userRepository = new();

    public ConfirmUserEmailHostAudienceGateTests()
    {
        _tokenService
            .Setup(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));
    }

    private ConfirmUserEmail.Handler Handler(string hostAudience) =>
        new(_tokenService.Object, _userRepository.Object, new HostAudienceProvider(hostAudience), new AuditContext());

    private (User user, string otp) UnconfirmedAccount(UserProfile profile)
    {
        var user = User.CreateWithPassword($"{profile}@example.com".ToLowerInvariant(), "Secret-123!", "First", "Last", profile);
        user.Created(user.Email, DateTime.UtcNow);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return (user, user.RawConfirmationToken!);
    }

    [Theory]
    [InlineData(JwtAudiences.Partner)]
    [InlineData(JwtAudiences.Mobile)]
    public async Task A_Partner_Host_Refuses_A_Customers_Code_And_Confirms_Nothing(string hostAudience)
    {
        var (customer, otp) = UnconfirmedAccount(UserProfile.Customer);

        var result = await Handler(hostAudience).Handle(new ConfirmUserEmail.Command(otp, customer.Email), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InsufficientPrivileges, result.Error!.Message);
        Assert.Equal(nameof(ConfirmUserEmail.Command.Email), result.Error.Code);
        Assert.False(customer.IsEmailConfirmed);
        Assert.NotNull(customer.ConfirmationCode);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(JwtAudiences.Partner, UserProfile.Employee)]
    [InlineData(JwtAudiences.Partner, UserProfile.Administrator)]
    [InlineData(JwtAudiences.Mobile, UserProfile.Employee)]
    [InlineData(JwtAudiences.Mobile, UserProfile.Administrator)]
    public async Task A_Partner_Host_Confirms_An_Employee_Or_Administrator(string hostAudience, UserProfile profile)
    {
        var (user, otp) = UnconfirmedAccount(profile);

        var result = await Handler(hostAudience).Handle(new ConfirmUserEmail.Command(otp, user.Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(user.IsEmailConfirmed);
        _tokenService.Verify(t => t.GenerateTokenAsync(user, true, hostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_Customer_Host_Still_Confirms_A_Customer()
    {
        var (customer, otp) = UnconfirmedAccount(UserProfile.Customer);

        var result = await Handler(JwtAudiences.Customer).Handle(new ConfirmUserEmail.Command(otp, customer.Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(customer.IsEmailConfirmed);
        _tokenService.Verify(t => t.GenerateTokenAsync(customer, true, JwtAudiences.Customer, It.IsAny<CancellationToken>()), Times.Once);
    }
}
