using Cleansia.TestUtilities.MockDataFactories.Users;

namespace Cleansia.Tests.Domain.Users;

public class UserLockoutStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsLockedOut_Is_False_When_No_Lockout_Was_Ever_Set()
    {
        var user = UserMockFactory.Generate();

        Assert.False(user.IsLockedOut(Now));
    }

    [Fact]
    public void IsLockedOut_Is_True_While_The_Window_Is_Open()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            LockoutEndsAt = Now.AddMinutes(5),
        });

        Assert.True(user.IsLockedOut(Now));
    }

    [Fact]
    public void IsLockedOut_Is_False_Once_The_Window_Has_Ended()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            LockoutEndsAt = Now,
        });

        Assert.False(user.IsLockedOut(Now));
        Assert.False(user.IsLockedOut(Now.AddSeconds(1)));
    }

    [Fact]
    public void ResetLoginThrottle_Clears_Counter_And_Lockout()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            FailedLoginAttempts = 3,
            LockoutEndsAt = Now.AddMinutes(5),
        });

        user.ResetLoginThrottle();

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEndsAt);
        Assert.False(user.IsLockedOut(Now));
    }

    [Fact]
    public void UpdateConfirmationCode_Grants_A_Fresh_Attempt_Budget()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            ConfirmationCodeAttempts = 4,
        });

        user.UpdateConfirmationCode();

        Assert.Equal(0, user.ConfirmationCodeAttempts);
    }

    [Fact]
    public void ConfirmEmail_Clears_The_Confirmation_Attempt_Counter()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            ConfirmationCodeAttempts = 2,
        });

        user.ConfirmEmail();

        Assert.Equal(0, user.ConfirmationCodeAttempts);
    }

    [Fact]
    public void UpdateResetPasswordToken_Grants_A_Fresh_Attempt_Budget()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            ResetPasswordCodeAttempts = 4,
        });

        user.UpdateResetPasswordToken();

        Assert.Equal(0, user.ResetPasswordCodeAttempts);
    }

    [Fact]
    public void ClearResetPasswordToken_Clears_The_Reset_Attempt_Counter()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            ResetPasswordCodeAttempts = 3,
        });

        user.ClearResetPasswordToken();

        Assert.Equal(0, user.ResetPasswordCodeAttempts);
    }

    [Fact]
    public void Anonymize_Clears_Lockout_And_Attempt_State()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            FailedLoginAttempts = 3,
            LockoutEndsAt = Now.AddMinutes(5),
            ConfirmationCodeAttempts = 4,
            ResetPasswordCodeAttempts = 4,
        });

        user.Anonymize();

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEndsAt);
        Assert.Equal(0, user.ConfirmationCodeAttempts);
        Assert.Equal(0, user.ResetPasswordCodeAttempts);
    }
}
