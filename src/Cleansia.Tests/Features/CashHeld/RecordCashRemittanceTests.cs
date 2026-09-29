using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.CashHeld;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.CashHeld;

/// <summary>
/// An administrator records cash a cleaner handed back (owner ruling 2026-09-28, decision 23): it comes
/// off what the cleaner holds in that currency, and never more than they hold.
/// </summary>
public sealed class RecordCashRemittanceTests
{
    private const string CleanerId = "emp-remit";
    private const string Czk = "cur-czk";
    private const string Eur = "cur-eur";
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<ICashLedgerRepository> _cashLedger = new();

    public RecordCashRemittanceTests()
    {
        _employees.Setup(r => r.ExistsAsync(CleanerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _currencies.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _cashLedger
            .Setup(r => r.GetBalancesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _cashLedger
            .Setup(r => r.GetBalancesAsync(CleanerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new CashHeldBalance(CleanerId, "Emp Loyee", Czk, "CZK", 1500m),
                new CashHeldBalance(CleanerId, "Emp Loyee", Eur, "EUR", 80m),
            ]);
    }

    private RecordCashRemittance.Validator Validator() =>
        new(_employees.Object, _currencies.Object, _cashLedger.Object);

    private static RecordCashRemittance.Command Command(
        decimal amount, string currencyId = Czk, string employeeId = CleanerId, string? note = null) =>
        new(employeeId, currencyId, amount, note);

    [Theory]
    [InlineData(1500)]
    [InlineData(0.01)]
    public async Task Up_To_The_Cash_Held_In_The_Currency_Is_Accepted(decimal amount)
    {
        var result = await Validator().ValidateAsync(Command(amount));

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Theory]
    [InlineData(1500.01, Czk)]
    [InlineData(81, Eur)]
    public async Task More_Than_The_Cash_Held_In_The_Currency_Is_Refused(decimal amount, string currencyId)
    {
        var result = await Validator().ValidateAsync(Command(amount, currencyId));

        Assert.Equal(BusinessErrorMessage.CashHeldAmountExceedsBalance, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Currency_The_Cleaner_Holds_None_Of_Takes_No_Remittance()
    {
        var result = await Validator().ValidateAsync(Command(1m, "cur-pln"));

        Assert.Equal(BusinessErrorMessage.CashHeldAmountExceedsBalance, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.005)]
    public async Task An_Amount_That_Is_Not_Money_Is_Refused(decimal amount)
    {
        var result = await Validator().ValidateAsync(Command(amount));

        Assert.Equal(BusinessErrorMessage.CashHeldAmountInvalid, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Cleaner_The_Company_Does_Not_Have_Is_Not_Found()
    {
        var result = await Validator().ValidateAsync(Command(10m, employeeId: "emp-elsewhere"));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotFound);
    }

    [Fact]
    public async Task The_Remittance_Takes_The_Amount_Off_What_The_Cleaner_Holds()
    {
        CashLedgerEntry? entered = null;
        _cashLedger.Setup(r => r.Add(It.IsAny<CashLedgerEntry>())).Callback<CashLedgerEntry>(e => entered = e);

        var result = await new RecordCashRemittance.Handler(_cashLedger.Object, new StubTimeProvider(Now))
            .Handle(Command(600m, note: "Deposited at the office"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(entered);
        Assert.Equal(entered!.Id, result.Value!.Id);
        Assert.Equal(
            (CleanerId, (string?)null, Czk, CashLedgerEntryKind.Remittance, -600m, Now.UtcDateTime, (string?)"Deposited at the office"),
            (entered.EmployeeId, entered.OrderId, entered.CurrencyId, entered.Kind, entered.Amount, entered.OccurredAt, entered.Note));
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
