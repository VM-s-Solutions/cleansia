using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The <see cref="GenerateInvoice.Handler"/> money aggregation: the created invoice sums the
/// fetched order pays exactly, every fetched pay is assigned to the new invoice so a second
/// generation can't double-bill, and a net-negative pay set clamps the total to zero.
/// Mocked repositories; no DB.
/// </summary>
public class GenerateInvoiceCommandHandlerTests
{
    private const string EmployeeId = PayrollMockFactory.EmployeeId;
    private const string PayPeriodId = PayrollMockFactory.PayPeriodId;

    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<IPayoutReferenceAllocator> _payoutReferenceAllocator = new();
    private readonly Mock<ICashLedgerRepository> _cashLedger = new();

    public GenerateInvoiceCommandHandlerTests()
    {
        _payoutReferenceAllocator
            .Setup(a => a.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(PayrollMockFactory.TestVariableSymbol));
        _payoutReferenceAllocator
            .Setup(a => a.AllocateInvoiceNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(PayrollMockFactory.TestInvoiceNumber));
    }

    private GenerateInvoice.Handler CreateHandler() => new(
        _invoiceRepository.Object,
        _orderPayRepository.Object,
        _payoutReferenceAllocator.Object,
        _cashLedger.Object);

    private void ArrangeOrderPays(params OrderEmployeePay[] pays) =>
        _orderPayRepository
            .Setup(r => r.GetUnassignedForEmployeePeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pays);

    [Fact]
    public async Task Sums_Order_Pays_Into_Invoice_Exactly()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 100.50m, extrasPay: 10m, expensesPay: 5.25m, bonusPay: 20m, deductionPay: 3m),
            PayrollMockFactory.OrderPay(basePay: 200m, extrasPay: 0m, expensesPay: 12.75m, bonusPay: 0m, deductionPay: 8m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(328.50m, added!.SubTotal); // (100.50+10+5.25) + (200+0+12.75)
        Assert.Equal(20m, added.BonusAmount);
        Assert.Equal(11m, added.DeductionAmount);
        Assert.Equal(2, added.TotalOrders);
        Assert.Equal(337.50m, added.TotalAmount); // 328.50 + 20 - 11
        _invoiceRepository.Verify(r => r.Add(It.IsAny<EmployeeInvoice>()), Times.Once);
    }

    [Fact]
    public async Task Assigns_Every_Fetched_Pay_To_The_New_Invoice()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 50m),
            PayrollMockFactory.OrderPay(basePay: 75m),
            PayrollMockFactory.OrderPay(basePay: 25m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(pays, p => Assert.Equal(added!.Id, p.EmployeeInvoiceId));
    }

    [Fact]
    public async Task Clamped_Pay_Is_Invoiced_At_Persisted_TotalPay_Not_Raw_Components()
    {
        // MaximumPay clamped the first order to 400 at calculation time (its components sum to
        // 650). The invoice must bill the persisted TotalPay — the same basis the period-pays
        // preview (GrandTotal = Σ TotalPay) shows the admin and the employee.
        var pays = new[]
        {
            ClampedOrderPay(basePay: 500m, extrasPay: 100m, expensesPay: 50m, clampedTotalPay: 400m),
            PayrollMockFactory.OrderPay(basePay: 100m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(pays.Sum(p => p.TotalPay), added!.TotalAmount);
        Assert.Equal(500m, added.SubTotal);
        Assert.Equal(500m, added.TotalAmount);
    }

    [Fact]
    public async Task Net_Negative_Pay_Set_Clamps_Invoice_Total_To_Zero()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 100m, bonusPay: 0m, deductionPay: 500m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, added!.TotalAmount);
    }

    /// <summary>
    /// The invoice number is a per-company series with the same yearly cap as the variable symbol, so
    /// its refusal must be the handler's refusal: no row is staged and no pay row is assigned to a
    /// document that does not exist, or the pay would be invoiced-without-an-invoice forever.
    /// </summary>
    [Fact]
    public async Task A_Refused_Invoice_Number_Refuses_The_Command_And_Stages_Nothing()
    {
        var pays = new[] { PayrollMockFactory.OrderPay(basePay: 100m), PayrollMockFactory.OrderPay(basePay: 50m) };
        ArrangeOrderPays(pays);
        _payoutReferenceAllocator
            .Setup(a => a.AllocateInvoiceNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<string>(new Error(
                nameof(EmployeeInvoice.InvoiceNumber),
                BusinessErrorMessage.InvoiceReferenceCapacityExhausted)));

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(EmployeeInvoice.InvoiceNumber), result.Error!.Code);
        Assert.Equal(BusinessErrorMessage.InvoiceReferenceCapacityExhausted, result.Error.Message);
        _invoiceRepository.Verify(r => r.Add(It.IsAny<EmployeeInvoice>()), Times.Never);
        Assert.All(pays, p => Assert.Null(p.EmployeeInvoiceId));
    }

    /// <summary>
    /// Owner ruling 2026-09-28, decision 23: an invoice issued by hand sets the cash the cleaner holds in
    /// its currency off against it exactly as the close does, and leaves its own amounts alone.
    /// </summary>
    [Fact]
    public async Task The_Cash_The_Cleaner_Holds_Is_Set_Off_Against_The_Invoice_Up_To_Its_Total()
    {
        ArrangeOrderPays(PayrollMockFactory.OrderPay(basePay: 400m));
        _cashLedger
            .Setup(r => r.GetHeldAsync(EmployeeId, PayrollMockFactory.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(650m);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>())).Callback<EmployeeInvoice>(i => added = i);
        CashLedgerEntry? entry = null;
        _cashLedger.Setup(r => r.Add(It.IsAny<CashLedgerEntry>())).Callback<CashLedgerEntry>(e => entry = e);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(400m, added!.TotalAmount);
        Assert.Equal(400m, added.CashSetOffAmount);
        Assert.Equal(0m, added.TransferAmount);
        Assert.NotNull(entry);
        Assert.Equal(CashLedgerEntryKind.SetOff, entry!.Kind);
        Assert.Equal(-400m, entry.Amount);
        Assert.Equal(added.InvoiceNumber, entry.Note);
    }

    [Fact]
    public async Task A_Cleaner_Who_Holds_No_Cash_Gets_No_Set_Off()
    {
        ArrangeOrderPays(PayrollMockFactory.OrderPay(basePay: 400m));
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>())).Callback<EmployeeInvoice>(i => added = i);

        await CreateHandler().Handle(new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.Equal(0m, added!.CashSetOffAmount);
        Assert.Equal(400m, added.TransferAmount);
        _cashLedger.Verify(r => r.Add(It.IsAny<CashLedgerEntry>()), Times.Never);
    }

    // PayrollMockFactory derives TotalPay from the components, so a pay where the min/max clamp
    // fired (TotalPay < components sum) has to be created directly.
    private static OrderEmployeePay ClampedOrderPay(
        decimal basePay, decimal extrasPay, decimal expensesPay, decimal clampedTotalPay) =>
        OrderEmployeePay.Create(
            orderId: $"order-{Guid.NewGuid():N}",
            employeeId: EmployeeId,
            payPeriodId: PayPeriodId,
            currencyId: PayrollMockFactory.CurrencyId,
            basePay: basePay,
            extrasPay: extrasPay,
            expensesPay: expensesPay,
            totalPay: clampedTotalPay);
}
