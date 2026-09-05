using Cleansia.Core.Domain.Credit;

namespace Cleansia.Tests.Features.Credit;

/// <summary>
/// When a credit balance dies, and what resets the clock.
///
/// <para>Owner ruling 2026-09-05: credit EXPIRES rather than being paid out. Cleansia does not do
/// Stripe payouts, so the alternative is a debt that sits on the books forever and a customer who
/// cannot be erased because of it. Twelve months from the customer's LAST movement.</para>
///
/// <para>The rule the tests below actually protect is the one the customer is shown: <b>every
/// movement resets the clock.</b> Get that wrong and a customer who was credited in January, spent
/// some in November and expected another year loses the rest two months later — which is the version
/// that generates a complaint nobody can answer.</para>
/// </summary>
public class CreditExpiryTests
{
    private const string Actor = "01ADMINCREDIT0000000000001";

    private static CreditAccount Account() =>
        CreditAccount.Create("01USERCREDIT00000000000001", "01CURRENCY0000000000000001", Actor);

    [Fact]
    public void AFreshAccountHasNoClockRunning()
    {
        Assert.Null(Account().ExpiresOn);
    }

    [Fact]
    public void TheFirstGrantStartsTheClock()
    {
        var account = Account();
        var before = DateTimeOffset.UtcNow;

        account.Issue(500m, CreditTransactionReason.Goodwill, "grant-1", Actor);

        Assert.NotNull(account.ExpiresOn);
        Assert.InRange(
            account.ExpiresOn!.Value,
            before.AddMonths(CreditAccount.ExpiryMonths).AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMonths(CreditAccount.ExpiryMonths).AddMinutes(1));
    }

    /// <summary>
    /// THE RULE THE CUSTOMER IS SHOWN. A second grant eleven months in must not leave the whole
    /// balance dying on the first grant's clock.
    /// </summary>
    [Fact]
    public void ASecondGrantPushesTheClockOut()
    {
        var account = Account();
        account.Issue(500m, CreditTransactionReason.Goodwill, "grant-1", Actor);
        var first = account.ExpiresOn;

        account.Issue(250m, CreditTransactionReason.DisputeSettlement, "grant-2", Actor);

        Assert.NotNull(first);
        Assert.True(
            account.ExpiresOn >= first,
            "a movement must never bring the expiry FORWARD");
    }

    [Fact]
    public void DrainingTakesTheWholeBalanceAndStopsTheClock()
    {
        var account = Account();
        account.Issue(500m, CreditTransactionReason.Goodwill, "grant-1", Actor);

        var taken = account.Drain(Actor, DateTimeOffset.UtcNow);

        Assert.Equal(500m, taken);
        Assert.Equal(0m, account.Balance);
        // An emptied account has no live balance and nothing to expire; the next grant starts fresh.
        Assert.Null(account.ExpiresOn);
    }

    [Fact]
    public void DrainingAnEmptyAccountTakesNothing()
    {
        var account = Account();

        Assert.Equal(0m, account.Drain(Actor, DateTimeOffset.UtcNow));
        Assert.Equal(0m, account.Balance);
    }

    [Fact]
    public void AGrantAfterAnExpiryStartsAFreshTwelveMonths()
    {
        var account = Account();
        account.Issue(500m, CreditTransactionReason.Goodwill, "grant-1", Actor);
        account.Drain(Actor, DateTimeOffset.UtcNow);

        account.Issue(300m, CreditTransactionReason.Goodwill, "grant-2", Actor);

        Assert.NotNull(account.ExpiresOn);
        Assert.Equal(300m, account.Balance);
    }

    /// <summary>
    /// THE INVARIANT, through the expiry: <c>Balance == SUM(Transactions.Amount)</c>. An expiry that
    /// zeroed the balance without a ledger row would leave the account permanently unreconcilable, and
    /// nothing would ever notice.
    /// </summary>
    [Fact]
    public void AnExpiryKeepsTheBalanceAndTheLedgerInAgreement()
    {
        var account = Account();
        account.Issue(500m, CreditTransactionReason.Goodwill, "grant-1", Actor);
        account.Issue(250m, CreditTransactionReason.CleanerNoShow, "grant-2", Actor);

        var taken = account.Drain(Actor, DateTimeOffset.UtcNow);
        account.RecordExpiry(taken, "credit-expired:acct:2026-09-06", Actor);

        Assert.Equal(0m, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
    }

    /// <summary>
    /// The expiry row is NEGATIVE, whatever the caller passes. A positive one would read as a grant
    /// and would break the invariant above in the direction nobody checks.
    /// </summary>
    [Fact]
    public void TheExpiryRowIsNegativeAndCarriesItsOwnReason()
    {
        var account = Account();
        account.Issue(500m, CreditTransactionReason.Goodwill, "grant-1", Actor);
        var taken = account.Drain(Actor, DateTimeOffset.UtcNow);

        var row = account.RecordExpiry(taken, "credit-expired:acct:2026-09-06", Actor, note: "customer asked");

        Assert.Equal(-500m, row.Amount);
        Assert.Equal(CreditTransactionReason.Expired, row.Reason);
        Assert.Equal("customer asked", row.Note);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnExpiryRowRefusesANonPositiveAmount(decimal amount)
    {
        var account = Account();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => account.RecordExpiry(amount, "k", Actor));
    }
}
