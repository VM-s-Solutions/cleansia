using Cleansia.Core.AppServices.Features.Credit;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Credit;

/// <summary>
/// One sweep crosses every account and balances are denominated, so what it took is reported PER
/// CURRENCY -- a single total would add crowns to euros (T-0702). The response is read by nothing but
/// the timer handler's log line, which is exactly why it must not carry a number that means nothing.
/// </summary>
public class ExpireStaleCreditHandlerTests
{
    private readonly Mock<ICreditAccountRepository> _accounts = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Expired_Balances_Are_Totalled_Per_Currency_And_Never_Added_Together()
    {
        var czk = CreditAccount.Create("user-1", "currency-czk", "admin");
        czk.Issue(500m, CreditTransactionReason.Goodwill, "k1", "admin");
        var eur = CreditAccount.Create("user-2", "currency-eur", "admin");
        eur.Issue(20m, CreditTransactionReason.Goodwill, "k2", "admin");
        SetExpired(czk);
        SetExpired(eur);
        _accounts
            .Setup(r => r.GetExpiredAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([czk, eur]);

        var result = await new ExpireStaleCredit.Handler(
                _accounts.Object, _tenantProvider.Object, _unitOfWork.Object, NullLogger<ExpireStaleCredit.Handler>.Instance)
            .Handle(new ExpireStaleCredit.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.AccountsExpired);
        Assert.Equal(2, result.Value.TotalExpiredByCurrencyId.Count);
        Assert.Equal(500m, result.Value.TotalExpiredByCurrencyId["currency-czk"]);
        Assert.Equal(20m, result.Value.TotalExpiredByCurrencyId["currency-eur"]);
        Assert.Equal(0m, czk.Balance);
        Assert.Equal(0m, eur.Balance);
        // One tenant group (both accounts are unstamped in-memory rows, so one empty key), one commit
        // per owner.
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Each_Owners_Lock_Is_Committed_Before_The_Next_Owner_Is_Locked()
    {
        var first = CreditAccount.Create("user-1", "currency-czk", "admin");
        first.Issue(500m, CreditTransactionReason.Goodwill, "k1", "admin");
        var firstEur = CreditAccount.Create("user-1", "currency-eur", "admin");
        firstEur.Issue(20m, CreditTransactionReason.Goodwill, "k2", "admin");
        var second = CreditAccount.Create("user-2", "currency-czk", "admin");
        second.Issue(30m, CreditTransactionReason.Goodwill, "k3", "admin");
        SetExpired(first);
        SetExpired(firstEur);
        SetExpired(second);
        _accounts.Setup(r => r.GetExpiredAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second, firstEur]);
        var events = new List<string>();
        _accounts.Setup(r => r.LockForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((userId, _) => events.Add($"lock:{userId}"))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => events.Add("commit"))
            .Returns(Task.CompletedTask);

        var result = await new ExpireStaleCredit.Handler(
                _accounts.Object, _tenantProvider.Object, _unitOfWork.Object, NullLogger<ExpireStaleCredit.Handler>.Instance)
            .Handle(new ExpireStaleCredit.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.AccountsExpired);
        Assert.Equal(new[] { "lock:user-1", "commit", "lock:user-2", "commit" }, events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Balance_Refreshed_After_Locking_Is_Not_Expired_From_The_Stale_Batch(bool erased)
    {
        var account = CreditAccount.Create("user-1", "currency-czk", "admin");
        account.Issue(100m, CreditTransactionReason.Goodwill, "grant", "admin");
        SetExpired(account);
        _accounts.Setup(r => r.GetExpiredAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([account]);
        _accounts.Setup(r => r.LockForUserAsync("user-1", It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // The repository reloads unchanged tracked accounts after the lock. Model the state
                // committed by erasure or a return while this sweep waited for that lock.
                if (erased)
                {
                    var amount = account.Drain("system", DateTimeOffset.UtcNow);
                    account.RecordExpiry(amount, $"account-deletion:{account.Id}", "system", "Account deletion");
                }
                else
                    account.Issue(25m, CreditTransactionReason.OrderPaymentReturned, "returned", "system");
            })
            .Returns(Task.CompletedTask);

        var result = await new ExpireStaleCredit.Handler(
                _accounts.Object, _tenantProvider.Object, _unitOfWork.Object, NullLogger<ExpireStaleCredit.Handler>.Instance)
            .Handle(new ExpireStaleCredit.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.AccountsExpired);
        Assert.Empty(result.Value.TotalExpiredByCurrencyId);
        Assert.Equal(erased ? 0m : 125m, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.Equal(2, account.Transactions.Count);
        Assert.DoesNotContain(account.Transactions, t => t.IdempotencyKey.StartsWith("credit-expired:"));
        _accounts.Verify(r => r.LockForUserAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static void SetExpired(CreditAccount account) =>
        typeof(CreditAccount).GetProperty(nameof(CreditAccount.ExpiresOn))!
            .SetValue(account, DateTimeOffset.UtcNow.AddDays(-1));
}
