using System.Diagnostics;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Users;

/// <summary>
/// Account pre-hijacking: a registration of an address nobody has confirmed, made again. The confirmation
/// proves who holds the mailbox, not which of the passwords typed for the address is theirs, so a second
/// password leaves the account with none and the owner who confirms it is left with no password anybody
/// else chose; the same password again is the same claim and stays.
/// </summary>
public class UserRegisterAgainTests
{
    private const string FirstPassword = "first-Passw0rd";
    private const string SecondPassword = "second-Passw0rd";

    [Fact]
    public void A_Registration_With_Another_Password_Leaves_The_Account_With_None()
    {
        var user = UnconfirmedWith(FirstPassword);

        user.RegisterAgain(SecondPassword);

        Assert.Null(user.Password);
    }

    [Fact]
    public void A_Registration_With_The_Same_Password_Keeps_It()
    {
        var user = UnconfirmedWith(FirstPassword);
        var stored = user.Password;

        user.RegisterAgain(FirstPassword);

        Assert.Equal(stored, user.Password);
        Assert.True(FirstPassword.VerifyPassword(user.Password!));
    }

    [Fact]
    public void A_Password_Withdrawn_Once_Is_Not_Brought_Back_By_Any_Later_Registration()
    {
        var user = UnconfirmedWith(FirstPassword);

        user.RegisterAgain(SecondPassword);
        user.RegisterAgain(FirstPassword);
        user.RegisterAgain(SecondPassword);

        Assert.Null(user.Password);
    }

    [Fact]
    public void Each_Registration_Issues_The_Code_The_Confirmation_Now_Takes_With_A_Fresh_Budget()
    {
        var user = UnconfirmedWith(FirstPassword);

        var raw = user.RegisterAgain(SecondPassword);

        Assert.Equal(SecurityTokens.Hash(raw), user.ConfirmationCode);
        Assert.Equal(raw, user.RawConfirmationToken);
        Assert.Equal(0, user.ConfirmationCodeAttempts);
        Assert.True(user.ConfirmationCodeExpiresAt > DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// A registration that skipped the derivation once the password is gone would tell the caller, by its
    /// speed alone, that a contested registration withdrew it. Fastest of three, so a busy machine can only
    /// make a sample slower, and the bar is a quarter of a registration that verifies a stored password.
    /// </summary>
    [Fact]
    public void A_Registration_After_The_Withdrawal_Takes_As_Long_As_One_That_Verifies_A_Stored_Password()
    {
        var stored = UnconfirmedWith(FirstPassword);
        var withdrawn = UnconfirmedWith(FirstPassword);
        withdrawn.RegisterAgain(SecondPassword);

        var verifying = Fastest(() => stored.RegisterAgain(FirstPassword));
        var afterWithdrawal = Fastest(() => withdrawn.RegisterAgain(FirstPassword));

        Assert.NotNull(stored.Password);
        Assert.Null(withdrawn.Password);
        Assert.True(afterWithdrawal >= verifying / 4, $"Registered again after the withdrawal in {afterWithdrawal.TotalMilliseconds} ms, with a stored password in {verifying.TotalMilliseconds} ms.");
    }

    private static TimeSpan Fastest(Action registration)
    {
        var fastest = TimeSpan.MaxValue;
        for (var sample = 0; sample < 3; sample++)
        {
            var clock = Stopwatch.StartNew();
            registration();
            clock.Stop();
            fastest = clock.Elapsed < fastest ? clock.Elapsed : fastest;
        }

        return fastest;
    }

    /// <summary>As the row is read back from the database, where the column holds the PBKDF2 hash.</summary>
    private static User UnconfirmedWith(string password) =>
        User.CreateWithPassword("owner@cleansia.test", password.HashAndSaltPassword(), "Jana", "Nováková");
}
