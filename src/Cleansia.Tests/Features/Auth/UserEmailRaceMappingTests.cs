using System.Reflection;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminUsers;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Azure.Storage.Queues;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// ADR-0050 §D2 — the four <see cref="User"/>-creating writers all read-then-insert with no lock, so
/// once <c>IX_Users_TenantId_Email</c> is armed with <c>NULLS NOT DISTINCT</c> the loser of a
/// simultaneous registration stops silently creating a second row and starts raising 23505. Each writer
/// flushes its own insert and maps that violation to the business error its own pre-check would have
/// produced; without the mapping the fix would trade a silent duplicate for an unhandled 500, which is
/// the worse user-facing outcome.
///
/// <para>The mapping keys on SQLSTATE 23505 alone. Each of these try/catch blocks wraps a deliberate
/// flush of ONE <see cref="User"/> insert, so the email index is the only unique index that can speak
/// there — a constraint-NAMED variant existed until 2026-08-14 and was removed as over-built, along
/// with the five tests that fed these flushes a collision on an unrelated index they cannot
/// produce.</para>
/// </summary>
public class UserEmailRaceMappingTests
{
    private const string Email = "racer@example.com";
    private const string Password = "Password1!@abc";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ICartRepository> _cartRepository = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly IPendingDispatch _pending = new InMemoryPendingDispatch();

    public UserEmailRaceMappingTests() =>
        _tokenService
            .Setup(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));

    private static DbUpdateException UniqueViolation() =>
        new("insert failed", new FakePostgresException("23505", constraintName: null));

    private void CommitThrows(DbUpdateException exception) =>
        _userRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

    private Register.Handler NewRegisterHandler() =>
        new(_cartRepository.Object, _userRepository.Object, new Mock<IReferralService>().Object, _pending,
            NullLogger<Register.Handler>.Instance);

    private RegisterEmployee.Handler NewRegisterEmployeeHandler() =>
        new(_cartRepository.Object, _userRepository.Object, new Mock<IEmployeeRepository>().Object, _pending);

    private GoogleAuth.Handler NewGoogleHandler(Mock<IGoogleTokenVerifier> verifier) =>
        new(verifier.Object, _tokenService.Object, _cartRepository.Object, _userRepository.Object,
            new HostAudienceProvider("customer"));

    private AppleAuth.Handler NewAppleHandler(Mock<IAppleTokenVerifier> verifier) =>
        new(verifier.Object, _tokenService.Object, _cartRepository.Object, _userRepository.Object,
            new HostAudienceProvider("customer"), NullLogger<AppleAuth.Handler>.Instance);

    private void AssertNoTokenMinted() =>
        _tokenService.Verify(
            t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private Mock<IGoogleTokenVerifier> NewGoogleVerifier()
    {
        var verifier = new Mock<IGoogleTokenVerifier>();
        verifier
            .Setup(v => v.VerifyAsync("token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims("google-subject", Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByGoogleIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        return verifier;
    }

    private Mock<IAppleTokenVerifier> NewAppleVerifier()
    {
        var verifier = new Mock<IAppleTokenVerifier>();
        verifier
            .Setup(v => v.VerifyAsync("token", "nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-subject", Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        return verifier;
    }

    private static Register.Command RegisterCommand() => new(Email, Password, "John", "Doe", "cs");

    private static RegisterEmployee.Command RegisterEmployeeCommand() => new(Email, Password, "John", "Doe", "cs");

    private static GoogleAuth.Command GoogleCommand() =>
        new(Token: "token", GoogleId: "ignored", Email: Email, FirstName: "John", LastName: "Doe",
            TermsAccepted: true);

    private static AppleAuth.Command AppleCommand() =>
        new(IdentityToken: "token", RawNonce: "nonce", FirstName: "John", LastName: "Doe",
            TermsAccepted: true);

    private static CreateAdminUser.Command AdminCommand() =>
        new(Email, Password, "John", "Doe", PhoneNumber: null, BirthDate: null, PreferredLanguageCode: null);

    private static async Task<BusinessResult<CreateAdminUser.Response>> InvokeCreateAdminUser(
        IUserRepository userRepository, CreateAdminUser.Command command)
    {
        var handlerType = typeof(CreateAdminUser).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, userRepository)!;
        var task = (Task<BusinessResult<CreateAdminUser.Response>>)handlerType!.GetMethod("Handle")!
            .Invoke(handler, [command, CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task Register_Maps_The_Email_Unique_Violation_To_ExistingUserWithEmail()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        CommitThrows(UniqueViolation());

        var result = await NewRegisterHandler().Handle(RegisterCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ExistingUserWithEmail, result.Error!.Message);
        Assert.Equal(nameof(Register.Command.Email), result.Error!.Code);
        // The loser must not also be told to confirm an account it did not create.
        Assert.Empty(_pending.Drain());
    }


    [Fact]
    public async Task Register_Does_Not_Flush_On_The_Re_Registration_Path()
    {
        var existing = User.CreateWithPassword(Email, Password, "John", "Doe");
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await NewRegisterHandler().Handle(RegisterCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // No row is inserted, so there is no race to arbitrate and the refreshed confirmation code
        // stays on the pipeline's single commit.
        _userRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterEmployee_Maps_The_Email_Unique_Violation_To_ExistingUserWithEmail()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        CommitThrows(UniqueViolation());

        var result = await NewRegisterEmployeeHandler().Handle(RegisterEmployeeCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ExistingUserWithEmail, result.Error!.Message);
        Assert.Equal(nameof(RegisterEmployee.Command.Email), result.Error!.Code);
        Assert.Empty(_pending.Drain());
    }


    [Fact]
    public async Task CreateAdminUser_Maps_The_Email_Unique_Violation_To_AdminUserEmailExists()
    {
        CommitThrows(UniqueViolation());

        var result = await InvokeCreateAdminUser(_userRepository.Object, AdminCommand());

        Assert.True(result.IsFailure);
        // The admin surface keeps its OWN message — the same one its pre-check produces.
        Assert.Equal(BusinessErrorMessage.AdminUserEmailExists, result.Error!.Message);
        Assert.Equal(nameof(CreateAdminUser.Command.Email), result.Error!.Code);
    }


    [Fact]
    public async Task GoogleAuth_Provisioning_Maps_The_Email_Unique_Violation_To_ExistingUserWithEmail()
    {
        var verifier = NewGoogleVerifier();
        CommitThrows(UniqueViolation());

        var result = await NewGoogleHandler(verifier).Handle(GoogleCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ExistingUserWithEmail, result.Error!.Message);
        Assert.Equal(nameof(GoogleAuth.Command.Email), result.Error!.Code);
        // The flush precedes the mint on purpose: a JWT handed out for a row the database refused would
        // authenticate a caller as an account that does not exist.
        AssertNoTokenMinted();
    }


    [Fact]
    public async Task AppleAuth_Provisioning_Maps_The_Email_Unique_Violation_To_ExistingUserWithEmail()
    {
        var verifier = NewAppleVerifier();
        CommitThrows(UniqueViolation());

        var result = await NewAppleHandler(verifier).Handle(AppleCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ExistingUserWithEmail, result.Error!.Message);
        Assert.Equal(nameof(AppleAuth.Command.IdentityToken), result.Error!.Code);
        AssertNoTokenMinted();
    }


    // The happy path still flushes exactly once; the pipeline's own commit is what persists everything
    // staged after the flush.
    [Fact]
    public async Task Every_Writer_Flushes_Its_Insert_Once_On_The_Happy_Path()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Assert.True((await NewRegisterHandler().Handle(RegisterCommand(), CancellationToken.None)).IsSuccess);
        Assert.True((await NewRegisterEmployeeHandler().Handle(RegisterEmployeeCommand(), CancellationToken.None)).IsSuccess);
        Assert.True((await InvokeCreateAdminUser(_userRepository.Object, AdminCommand())).IsSuccess);
        Assert.True((await NewGoogleHandler(NewGoogleVerifier()).Handle(GoogleCommand(), CancellationToken.None)).IsSuccess);
        Assert.True((await NewAppleHandler(NewAppleVerifier()).Handle(AppleCommand(), CancellationToken.None)).IsSuccess);

        _userRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(5));
    }

    private sealed class FakePostgresException(string sqlState, string? constraintName)
        : Exception("postgres")
    {
        public string SqlState { get; } = sqlState;

        public string? ConstraintName { get; } = constraintName;
    }
}
