using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Azure.Storage.Queues;
using Cleansia.Tests.Domain.Legal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Account pre-hijacking, on both registration paths: registering again an address nobody has confirmed
/// used to keep the first registrant's password, so whoever registered someone else's address first still
/// signed in once the real owner confirmed it. A second registration with another password now leaves the
/// account with none; the same password again keeps it.
/// </summary>
public class RegisterAgainPasswordTests
{
    private const string Email = "owner@cleansia.test";
    private const string FirstPassword = "first-Passw0rd";
    private const string SecondPassword = "second-Passw0rd";

    private readonly Mock<IUserRepository> _userRepository = new();

    [Theory]
    [InlineData(SecondPassword, false)]
    [InlineData(FirstPassword, true)]
    public async Task A_Customer_Registering_Again_Keeps_The_Password_Only_When_It_Is_The_Same(string password, bool kept)
    {
        var user = UnconfirmedWith(FirstPassword, UserProfile.Customer);

        var result = await new Register.Handler(
                _userRepository.Object, Mock.Of<IReferralService>(), new InMemoryPendingDispatch(),
                Mock.Of<IConsentService>(), LegalDocumentFixtures.Resolver(null, null).Object, new AuditContext(),
                NullLogger<Register.Handler>.Instance)
            .Handle(new Register.Command(Email, password, "Jana", "Nováková", "cs", TermsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(kept, user.Password is not null && FirstPassword.VerifyPassword(user.Password));
    }

    [Theory]
    [InlineData(SecondPassword, false)]
    [InlineData(FirstPassword, true)]
    public async Task A_Cleaner_Registering_Again_Keeps_The_Password_Only_When_It_Is_The_Same(string password, bool kept)
    {
        var user = UnconfirmedWith(FirstPassword, UserProfile.Employee);

        var result = await new RegisterEmployee.Handler(
                _userRepository.Object, Mock.Of<IEmployeeRepository>(), new InMemoryPendingDispatch())
            .Handle(new RegisterEmployee.Command(Email, password, "Jana", "Nováková", "cs"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(kept, user.Password is not null && FirstPassword.VerifyPassword(user.Password));
    }

    /// <summary>The row as the validator and the handler read it back: unconfirmed, its password column hashed.</summary>
    private User UnconfirmedWith(string password, UserProfile profile)
    {
        var user = User.CreateWithPassword(Email, password.HashAndSaltPassword(), "Jana", "Nováková", profile);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return user;
    }
}
