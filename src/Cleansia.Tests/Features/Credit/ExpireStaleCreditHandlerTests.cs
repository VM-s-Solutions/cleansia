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
        // One tenant group (both single-tenant), one commit.
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
