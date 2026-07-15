using System.Reflection;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database.Converters;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Authenticated change-own-password on the admin host. The subject is ALWAYS the JWT caller
/// (the command carries no user id — [OWN-DATA]); a correct current password rotates the
/// credential, a wrong one is rejected and the stored password is untouched.
///
/// The hashing contract matches the login path exactly: the handler stores the RAW new password
/// and the EF <see cref="PasswordConverter"/> hashes exactly once on persist — pre-hashing here
/// would recreate the hash(hash(password)) lockout the admin-create flow once shipped, so the
/// round-trip test runs the entity value through the REAL converter and verifies with
/// <see cref="PasswordExtensions.VerifyPassword"/>.
/// </summary>
public class ChangeOwnPasswordTests
{
    private const string CallerId = "admin-caller";
    private const string OtherAdminId = "admin-other";
    private const string CurrentPassword = "Curr3ntPass";
    private const string NewPassword = "BrandNew123";

    private static readonly Func<object?, object?> WriteConversion = new PasswordConverter().ConvertToProvider;

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefreshTokenService> _refreshTokens = new();

    private static User BuildAdmin(string id, string rawPassword)
    {
        var user = User.CreateWithPassword($"{id}@example.com", rawPassword, "First", "Last", UserProfile.Administrator);
        user.Id = id;
        // Model the persisted column: the DB holds the salted hash, never the raw password.
        user.UpdatePassword(rawPassword.HashAndSaltPassword());
        return user;
    }

    private User ArrangeCaller()
    {
        var caller = BuildAdmin(CallerId, CurrentPassword);
        _session.Setup(s => s.GetUserId()).Returns(CallerId);
        _userRepository
            .Setup(r => r.GetByIdAsync(CallerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        return caller;
    }

    [Fact]
    public async Task Correct_Current_Password_Rotates_It_And_The_New_One_Verifies_Through_The_Converter()
    {
        var caller = ArrangeCaller();

        var result = await InvokeHandler(new ChangeOwnPassword.Command(CurrentPassword, NewPassword));

        Assert.True(result.IsSuccess);
        // The entity must hold the RAW new password — the converter is the single hashing step.
        Assert.Equal(NewPassword, caller.Password);
        var stored = (string?)WriteConversion(caller.Password);
        Assert.True(NewPassword.VerifyPassword(stored!),
            "The new password must verify against the single-hashed stored value (login-path compare).");
        Assert.False(CurrentPassword.VerifyPassword(stored!),
            "The old password must no longer verify.");
    }

    [Fact]
    public async Task Wrong_Current_Password_Fails_With_CurrentPasswordInvalid_And_Password_Is_Unchanged()
    {
        var caller = ArrangeCaller();
        var storedBefore = caller.Password;

        var result = await InvokeHandler(new ChangeOwnPassword.Command("WrongPass1", NewPassword));

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CurrentPasswordInvalid, result.Error!.Message);
        Assert.Equal(storedBefore, caller.Password);
    }

    // A wrong current-password attempt is charged to the account's lockout budget (the same
    // pair the login surfaces use, so a change-password sprayer also bounds login).
    [Fact]
    public async Task Wrong_Current_Password_Charges_The_Account_Lockout_Budget()
    {
        ArrangeCaller();

        var result = await InvokeHandler(new ChangeOwnPassword.Command("WrongPass1", NewPassword));

        Assert.True(result.IsFailure);
        _userRepository.Verify(
            r => r.RecordFailedCurrentPasswordAttemptAsync(CallerId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Once the budget is exhausted (lockout window open) the current password is no longer
    // evaluated: the distinct AccountLocked key is returned and no further charge is made (no oracle).
    [Fact]
    public async Task Locked_Account_Refuses_Even_The_Correct_Current_Password_Without_Evaluating_It()
    {
        var caller = BuildAdmin(CallerId, CurrentPassword);
        caller.Merge(new UserMockFactory.UserPartial { LockoutEndsAt = DateTimeOffset.UtcNow.AddMinutes(10) });
        var storedBefore = caller.Password;
        _session.Setup(s => s.GetUserId()).Returns(CallerId);
        _userRepository.Setup(r => r.GetByIdAsync(CallerId, It.IsAny<CancellationToken>())).ReturnsAsync(caller);

        var result = await InvokeHandler(new ChangeOwnPassword.Command(CurrentPassword, NewPassword));

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.AccountLocked, result.Error!.Message);
        Assert.Equal(storedBefore, caller.Password);
        _userRepository.Verify(
            r => r.RecordFailedCurrentPasswordAttemptAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // A successful change restores a fresh budget by resetting the login throttle.
    [Fact]
    public async Task Successful_Change_Resets_The_Lockout_Budget()
    {
        var caller = BuildAdmin(CallerId, CurrentPassword);
        caller.Merge(new UserMockFactory.UserPartial { FailedLoginAttempts = 3 });
        _session.Setup(s => s.GetUserId()).Returns(CallerId);
        _userRepository.Setup(r => r.GetByIdAsync(CallerId, It.IsAny<CancellationToken>())).ReturnsAsync(caller);

        var result = await InvokeHandler(new ChangeOwnPassword.Command(CurrentPassword, NewPassword));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, caller.FailedLoginAttempts);
        Assert.Null(caller.LockoutEndsAt);
    }

    // AC4 — the handler resolves the subject from the session only; another admin's row is
    // never read or mutated, whatever the request body contains.
    [Fact]
    public async Task Caller_Cannot_Target_Another_Admin()
    {
        var caller = ArrangeCaller();
        var other = BuildAdmin(OtherAdminId, "Other1pass");
        var otherStoredBefore = other.Password;
        _userRepository
            .Setup(r => r.GetByIdAsync(OtherAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(other);

        var result = await InvokeHandler(new ChangeOwnPassword.Command(CurrentPassword, NewPassword));

        Assert.True(result.IsSuccess);
        Assert.Equal(NewPassword, caller.Password);
        Assert.Equal(otherStoredBefore, other.Password);
        _userRepository.Verify(r => r.GetByIdAsync(CallerId, It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(r => r.GetByIdAsync(OtherAdminId, It.IsAny<CancellationToken>()), Times.Never);
    }

    // The wire shape itself offers no field an attacker could point at another account —
    // CurrentRefreshToken is the caller's OWN cookie value (server-enriched), and the revocation
    // it feeds is scoped to the caller's userId, so it cannot reach another account either way.
    [Fact]
    public void Command_Carries_No_Client_Supplied_Subject_Identifier()
    {
        var propertyNames = typeof(ChangeOwnPassword.Command)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(["CurrentPassword", "CurrentRefreshToken", "NewPassword"], propertyNames);
    }

    // A rotated credential must end every session the old one minted — except the session
    // performing the change (revoke all-OTHER; the reset flow is the revoke-ALL counterpart).
    [Fact]
    public async Task Successful_Change_Revokes_All_Other_Sessions_Sparing_The_Callers()
    {
        ArrangeCaller();
        const string callerRefreshToken = "raw-refresh-of-the-changing-session";

        var result = await InvokeHandler(
            new ChangeOwnPassword.Command(CurrentPassword, NewPassword, callerRefreshToken));

        Assert.True(result.IsSuccess);
        _refreshTokens.Verify(
            s => s.RevokeAllForUserAsync(CallerId, "password_changed", callerRefreshToken, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Wrong_Current_Password_Revokes_No_Sessions()
    {
        ArrangeCaller();

        var result = await InvokeHandler(new ChangeOwnPassword.Command("WrongPass1", NewPassword));

        Assert.True(result.IsFailure);
        _refreshTokens.Verify(
            s => s.RevokeAllForUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Locked_Account_Revokes_No_Sessions()
    {
        var caller = BuildAdmin(CallerId, CurrentPassword);
        caller.Merge(new UserMockFactory.UserPartial { LockoutEndsAt = DateTimeOffset.UtcNow.AddMinutes(10) });
        _session.Setup(s => s.GetUserId()).Returns(CallerId);
        _userRepository.Setup(r => r.GetByIdAsync(CallerId, It.IsAny<CancellationToken>())).ReturnsAsync(caller);

        var result = await InvokeHandler(new ChangeOwnPassword.Command(CurrentPassword, NewPassword));

        Assert.True(result.IsFailure);
        _refreshTokens.Verify(
            s => s.RevokeAllForUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Permission_Maps_To_Authenticated()
    {
        Assert.Equal(PhysicalPolicy.Authenticated, Policy.CanChangeOwnPassword.ToPhysicalPolicy());
    }

    [Fact]
    public async Task Weak_New_Password_Fails_With_InvalidPasswordFormat()
    {
        var validator = new ChangeOwnPassword.Validator();

        var result = await validator.ValidateAsync(new ChangeOwnPassword.Command(CurrentPassword, "short1"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidPasswordFormat);
    }

    [Fact]
    public async Task Empty_Current_Password_Fails_With_Required()
    {
        var validator = new ChangeOwnPassword.Validator();

        var result = await validator.ValidateAsync(new ChangeOwnPassword.Command(string.Empty, NewPassword));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task Valid_Command_Passes_Validation()
    {
        var validator = new ChangeOwnPassword.Validator();

        var result = await validator.ValidateAsync(new ChangeOwnPassword.Command(CurrentPassword, NewPassword));

        Assert.True(result.IsValid);
    }

    private async Task<BusinessResult<ChangeOwnPassword.Response>> InvokeHandler(ChangeOwnPassword.Command command)
    {
        var handlerType = typeof(ChangeOwnPassword).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, _userRepository.Object, _session.Object, _refreshTokens.Object)!;
        var task = (Task<BusinessResult<ChangeOwnPassword.Response>>)handlerType!.GetMethod("Handle")!
            .Invoke(handler, [command, CancellationToken.None])!;
        return await task;
    }
}
