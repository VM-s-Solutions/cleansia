using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.CashHeld;
using Cleansia.Core.AppServices.Features.CashHeld.DTOs;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.CashHeld;

/// <summary>
/// "Cash I hold" on the partner apps (owner ruling 2026-09-28, decision 23): the calling cleaner's own
/// cash per currency, resolved from the session and never from the request; a caller who is not a cleaner
/// holds none.
/// </summary>
public sealed class GetMyCashHeldTests
{
    private readonly Mock<ICashLedgerRepository> _cashLedger = new();
    private readonly Mock<IOrderAccessService> _access = new();

    private GetMyCashHeld.Handler Handler() => new(_cashLedger.Object, _access.Object);

    [Fact]
    public async Task The_Cleaner_Reads_Their_Own_Cash_Per_Currency()
    {
        _access.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync("emp-me");
        _cashLedger
            .Setup(r => r.GetBalancesAsync("emp-me", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new CashHeldBalance("emp-me", "Emp Loyee", "cur-czk", "CZK", 2450m),
                new CashHeldBalance("emp-me", "Emp Loyee", "cur-eur", "EUR", 90m),
            ]);

        var result = await Handler().Handle(new GetMyCashHeld.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            [new CashHeldDto("cur-czk", "CZK", 2450m), new CashHeldDto("cur-eur", "EUR", 90m)],
            result.Value!);
    }

    [Fact]
    public async Task A_Caller_Who_Is_Not_A_Cleaner_Holds_None_And_Reads_No_Ledger()
    {
        _access.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var result = await Handler().Handle(new GetMyCashHeld.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!);
        _cashLedger.Verify(r => r.GetBalancesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
