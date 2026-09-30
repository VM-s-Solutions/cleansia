using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.CashHeld;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.CashHeld;

/// <summary>
/// An administrator writes off cash a cleaner will not hand back (owner ruling 2026-09-28, decision 23):
/// never more than they hold, and never without a note saying why.
/// </summary>
public sealed class WriteOffCashHeldTests
{
    private const string CleanerId = "emp-write-off";
    private const string Czk = "cur-czk";
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<ICashLedgerRepository> _cashLedger = new();

    public WriteOffCashHeldTests()
    {
        _employees.Setup(r => r.ExistsAsync(CleanerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _currencies.Setup(r => r.ExistsAsync(Czk, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _cashLedger
            .Setup(r => r.GetBalancesAsync(CleanerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CashHeldBalance(CleanerId, "Emp Loyee", Czk, "CZK", 400m)]);
    }

    private WriteOffCashHeld.Validator Validator() => new(_employees.Object, _currencies.Object, _cashLedger.Object);

    [Fact]
    public async Task The_Whole_Cash_Held_With_A_Note_Is_Accepted()
    {
        var result = await Validator().ValidateAsync(new WriteOffCashHeld.Command(CleanerId, Czk, 400m, "Cleaner left, unreachable"));

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task More_Than_The_Cash_Held_Is_Refused()
    {
        var result = await Validator().ValidateAsync(new WriteOffCashHeld.Command(CleanerId, Czk, 400.01m, "Note"));

        Assert.Equal(BusinessErrorMessage.CashHeldAmountExceedsBalance, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task A_Write_Off_Without_A_Note_Is_Refused(string note)
    {
        var result = await Validator().ValidateAsync(new WriteOffCashHeld.Command(CleanerId, Czk, 100m, note));

        Assert.Equal(BusinessErrorMessage.Required, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task An_Unknown_Currency_Is_Refused()
    {
        var result = await Validator().ValidateAsync(new WriteOffCashHeld.Command(CleanerId, "cur-none", 100m, "Note"));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CurrencyNotFound);
    }

    [Fact]
    public async Task The_Write_Off_Takes_The_Amount_Off_What_The_Cleaner_Holds_With_Its_Note()
    {
        CashLedgerEntry? entered = null;
        _cashLedger
            .Setup(r => r.TryDebitAsync(It.IsAny<CashLedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<CashLedgerEntry, CancellationToken>((e, _) => entered = e)
            .ReturnsAsync(true);

        var result = await new WriteOffCashHeld.Handler(_cashLedger.Object, new StubTimeProvider(Now))
            .Handle(new WriteOffCashHeld.Command(CleanerId, Czk, 400m, "Cleaner left, unreachable"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(entered);
        Assert.Equal(
            (CleanerId, Czk, CashLedgerEntryKind.WriteOff, -400m, Now.UtcDateTime, (string?)"Cleaner left, unreachable"),
            (entered!.EmployeeId, entered.CurrencyId, entered.Kind, entered.Amount, entered.OccurredAt, entered.Note));
    }

    [Fact]
    public async Task Cash_Taken_Off_By_A_Debit_That_Landed_After_Validation_Refuses_The_Write_Off()
    {
        _cashLedger
            .Setup(r => r.TryDebitAsync(It.IsAny<CashLedgerEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await new WriteOffCashHeld.Handler(_cashLedger.Object, new StubTimeProvider(Now))
            .Handle(new WriteOffCashHeld.Command(CleanerId, Czk, 400m, "Cleaner left, unreachable"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.CashHeldAmountExceedsBalance, result.Error!.Message);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
