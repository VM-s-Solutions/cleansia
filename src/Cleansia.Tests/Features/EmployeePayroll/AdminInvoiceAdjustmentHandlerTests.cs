using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// AC1/AC2 (T-0171a) — the admin invoice adjustment and dispute handlers. The domain guards
/// the Paid terminal state (<see cref="EmployeeInvoice.UpdateAmounts"/> /
/// <see cref="EmployeeInvoice.Dispute"/>); these tests pin that each handler drives the domain
/// transition on the happy path and surfaces a BusinessResult error (no throw, no mutation) on the
/// guarded Paid case. Adjust changes only a Pending or Disputed invoice and renders its PDF again once the
/// new amounts are saved, and approving keeps the note written through Adjust.
/// </summary>
public class AdminInvoiceAdjustmentHandlerTests
{
    private const string InvoiceId = "invoice-1";

    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<ICashLedgerRepository> _cashLedger = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();

    public AdminInvoiceAdjustmentHandlerTests()
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(IMediator))).Returns(_mediator.Object);
        services.Setup(s => s.GetService(typeof(ITenantProvider))).Returns(new Mock<ITenantProvider>().Object);
        _scopeFactory.Setup(f => f.CreateScope()).Returns(() =>
        {
            var scope = new Mock<IServiceScope>();
            scope.SetupGet(s => s.ServiceProvider).Returns(services.Object);
            return scope.Object;
        });
    }

    private UpdateInvoiceAmounts.Handler AdjustHandler() =>
        new(_invoiceRepository.Object, _cashLedger.Object, _scopeFactory.Object, NullLogger<UpdateInvoiceAmounts.Handler>.Instance);

    private static EmployeeInvoice PendingInvoice()
    {
        var invoice = EmployeeInvoice.Create(
            employeeId: "emp-1",
            payPeriodId: "period-1",
            totalOrders: 2,
            subTotal: 200m,
            currencyId: "currency-1",
            variableSymbol: PayrollMockFactory.TestVariableSymbol,
            invoiceNumber: PayrollMockFactory.TestInvoiceNumber);
        invoice.Id = InvoiceId;
        return invoice;
    }

    private static EmployeeInvoice PaidInvoice()
    {
        var invoice = PendingInvoice();
        invoice.Approve("admin@cleansia.cz");
        invoice.MarkAsPaid();
        return invoice;
    }

    private void Arrange(EmployeeInvoice invoice) =>
        _invoiceRepository
            .Setup(r => r.GetByIdAsync(InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

    // ── UpdateInvoiceAmounts (AC1) ───────────────────────────────────

    [Fact]
    public async Task UpdateInvoiceAmounts_Recomputes_Total_On_Pending_Invoice()
    {
        var invoice = PendingInvoice();
        Arrange(invoice);
        var handler = AdjustHandler();

        var result = await handler.Handle(
            new UpdateInvoiceAmounts.Command(InvoiceId, BonusAmount: 50m, DeductionAmount: 30m, AdminNotes: "correction"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50m, invoice.BonusAmount);
        Assert.Equal(30m, invoice.DeductionAmount);
        Assert.Equal(220m, invoice.TotalAmount); // 200 + 50 - 30
        Assert.Equal("correction", invoice.AdminNotes);
    }

    [Fact]
    public async Task UpdateInvoiceAmounts_Clamps_Negative_Total_To_Zero()
    {
        var invoice = PendingInvoice();
        Arrange(invoice);
        var handler = AdjustHandler();

        var result = await handler.Handle(
            new UpdateInvoiceAmounts.Command(InvoiceId, BonusAmount: 0m, DeductionAmount: 500m, AdminNotes: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, invoice.TotalAmount);
    }

    [Fact]
    public async Task UpdateInvoiceAmounts_Lowering_The_Total_Below_The_Cash_Set_Off_Gives_The_Difference_Back_To_The_Cleaner()
    {
        var invoice = PendingInvoice();
        invoice.SetOffCash(200m);
        Arrange(invoice);
        CashLedgerEntry? entry = null;
        _cashLedger.Setup(r => r.Add(It.IsAny<CashLedgerEntry>())).Callback<CashLedgerEntry>(e => entry = e);

        var result = await AdjustHandler().Handle(
            new UpdateInvoiceAmounts.Command(InvoiceId, BonusAmount: 0m, DeductionAmount: 50m, AdminNotes: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(150m, invoice.TotalAmount);
        Assert.Equal(150m, invoice.CashSetOffAmount);
        Assert.Equal(0m, invoice.TransferAmount);
        Assert.NotNull(entry);
        Assert.Equal(CashLedgerEntryKind.SetOff, entry!.Kind);
        Assert.Equal("emp-1", entry.EmployeeId);
        Assert.Equal("currency-1", entry.CurrencyId);
        Assert.Equal(50m, entry.Amount);
        Assert.Equal(invoice.InvoiceNumber, entry.Note);
    }

    [Fact]
    public async Task UpdateInvoiceAmounts_On_Paid_Invoice_Returns_Error_And_Does_Not_Mutate()
    {
        var invoice = PaidInvoice();
        var bonusBefore = invoice.BonusAmount;
        var totalBefore = invoice.TotalAmount;
        Arrange(invoice);
        var handler = AdjustHandler();

        var result = await handler.Handle(
            new UpdateInvoiceAmounts.Command(InvoiceId, BonusAmount: 99m, DeductionAmount: 0m, AdminNotes: "x"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvoiceAlreadyPaid, result.Error!.Message);
        Assert.Equal(bonusBefore, invoice.BonusAmount);
        Assert.Equal(totalBefore, invoice.TotalAmount);
    }

    // ── DisputeInvoice (AC2) ─────────────────────────────────────────

    [Fact]
    public async Task DisputeInvoice_Moves_Pending_To_Disputed_With_Notes()
    {
        var invoice = PendingInvoice();
        Arrange(invoice);
        var handler = new DisputeInvoice.Handler(_invoiceRepository.Object);

        var result = await handler.Handle(
            new DisputeInvoice.Command(InvoiceId, AdminNotes: "amount disputed"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeInvoiceStatus.Disputed, invoice.Status);
        Assert.Equal("amount disputed", invoice.AdminNotes);
    }

    [Fact]
    public async Task DisputeInvoice_On_Paid_Invoice_Returns_Error_And_Does_Not_Mutate()
    {
        var invoice = PaidInvoice();
        Arrange(invoice);
        var handler = new DisputeInvoice.Handler(_invoiceRepository.Object);

        var result = await handler.Handle(
            new DisputeInvoice.Command(InvoiceId, AdminNotes: "x"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvoiceAlreadyPaid, result.Error!.Message);
        Assert.Equal(EmployeeInvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public async Task DisputeInvoice_Is_Refused_On_A_Cancelled_Invoice()
    {
        Arrange(InvoiceIn(EmployeeInvoiceStatus.Cancelled));
        _invoiceRepository.Setup(r => r.ExistsAsync(InvoiceId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new DisputeInvoice.Validator(_invoiceRepository.Object)
            .ValidateAsync(new DisputeInvoice.Command(InvoiceId, AdminNotes: "x"));

        Assert.Equal(BusinessErrorMessage.InvoiceAlreadyCancelled, Assert.Single(result.Errors).ErrorMessage);
    }

    // ── Adjust renders the PDF again, and only on a Pending or Disputed invoice ──

    [Fact]
    public async Task UpdateInvoiceAmounts_Renders_The_Pdf_Again_Once_The_New_Amounts_Are_Saved()
    {
        var invoice = PendingInvoice();
        Arrange(invoice);
        var steps = new List<string>();
        _invoiceRepository.Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add($"save {invoice.TotalAmount}"))
            .Returns(Task.CompletedTask);
        _mediator.Setup(m => m.Send(It.IsAny<RegenerateInvoicePdf.Command>(), It.IsAny<CancellationToken>()))
            .Returns<RegenerateInvoicePdf.Command, CancellationToken>((command, _) =>
            {
                steps.Add($"render {command.InvoiceId}");
                return Task.FromResult(BusinessResult.Success(new RegenerateInvoicePdf.Response("invoice.pdf")));
            });

        var result = await AdjustHandler().Handle(
            new UpdateInvoiceAmounts.Command(InvoiceId, BonusAmount: 50m, DeductionAmount: 30m, AdminNotes: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["save 220", $"render {InvoiceId}"], steps);
    }

    [Theory]
    [InlineData(EmployeeInvoiceStatus.Approved)]
    [InlineData(EmployeeInvoiceStatus.Cancelled)]
    public async Task UpdateInvoiceAmounts_Is_Refused_Unless_The_Invoice_Is_Pending_Or_Disputed(EmployeeInvoiceStatus status)
    {
        Arrange(InvoiceIn(status));

        var result = await AdjustValidator().ValidateAsync(new UpdateInvoiceAmounts.Command(InvoiceId, 10m, 0m, null));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.InvalidInvoiceStatus, error.ErrorMessage);
    }

    [Theory]
    [InlineData(EmployeeInvoiceStatus.Pending)]
    [InlineData(EmployeeInvoiceStatus.Disputed)]
    public async Task UpdateInvoiceAmounts_Is_Allowed_On_A_Pending_Or_Disputed_Invoice(EmployeeInvoiceStatus status)
    {
        Arrange(InvoiceIn(status));

        var result = await AdjustValidator().ValidateAsync(new UpdateInvoiceAmounts.Command(InvoiceId, 10m, 0m, null));

        Assert.True(result.IsValid);
    }

    // ── ApproveInvoice keeps the note written through Adjust ─────────

    [Fact]
    public async Task Approving_A_Disputed_Invoice_Keeps_The_Note_Written_Through_Adjust()
    {
        var invoice = InvoiceIn(EmployeeInvoiceStatus.Disputed);
        Arrange(invoice);
        await AdjustHandler().Handle(
            new UpdateInvoiceAmounts.Command(InvoiceId, BonusAmount: 50m, DeductionAmount: 0m, AdminNotes: "bonus agreed on the phone"),
            CancellationToken.None);
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserEmail()).Returns("admin@cleansia.cz");

        var result = await new ApproveInvoice.Handler(session.Object, _invoiceRepository.Object).Handle(
            new ApproveInvoice.Command(InvoiceId, AdminNotes: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeInvoiceStatus.Approved, invoice.Status);
        Assert.Equal("bonus agreed on the phone", invoice.AdminNotes);
    }

    private UpdateInvoiceAmounts.Validator AdjustValidator()
    {
        _invoiceRepository.Setup(r => r.ExistsAsync(InvoiceId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return new UpdateInvoiceAmounts.Validator(_invoiceRepository.Object);
    }

    private static EmployeeInvoice InvoiceIn(EmployeeInvoiceStatus status)
    {
        var invoice = PendingInvoice();
        switch (status)
        {
            case EmployeeInvoiceStatus.Approved:
                invoice.Approve("admin@cleansia.cz");
                break;
            case EmployeeInvoiceStatus.Disputed:
                invoice.Dispute("The cleaner disputes the amount");
                break;
            case EmployeeInvoiceStatus.Cancelled:
                invoice.Cancel("Wrong period", "admin-1");
                break;
        }

        Assert.Equal(status, invoice.Status);
        return invoice;
    }
}
