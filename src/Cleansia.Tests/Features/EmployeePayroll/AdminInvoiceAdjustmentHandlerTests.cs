using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// AC1/AC2 (T-0171a) — the admin invoice adjustment + dispute/reject handlers. The domain guards
/// the Paid terminal state (<see cref="EmployeeInvoice.UpdateAmounts"/> /
/// <see cref="EmployeeInvoice.Dispute"/> / <see cref="EmployeeInvoice.Reject"/>); these tests pin
/// that each handler drives the domain transition on the happy path and surfaces a BusinessResult
/// error (no throw, no mutation) on the guarded Paid case. Written red → green per knowledge/testing.md.
/// </summary>
public class AdminInvoiceAdjustmentHandlerTests
{
    private const string InvoiceId = "invoice-1";

    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();

    private static EmployeeInvoice PendingInvoice()
    {
        var invoice = EmployeeInvoice.Create(
            employeeId: "emp-1",
            payPeriodId: "period-1",
            totalOrders: 2,
            subTotal: 200m,
            currencyId: "currency-1");
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
        var handler = new UpdateInvoiceAmounts.Handler(_invoiceRepository.Object);

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
        var handler = new UpdateInvoiceAmounts.Handler(_invoiceRepository.Object);

        var result = await handler.Handle(
            new UpdateInvoiceAmounts.Command(InvoiceId, BonusAmount: 0m, DeductionAmount: 500m, AdminNotes: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, invoice.TotalAmount);
    }

    [Fact]
    public async Task UpdateInvoiceAmounts_On_Paid_Invoice_Returns_Error_And_Does_Not_Mutate()
    {
        var invoice = PaidInvoice();
        var bonusBefore = invoice.BonusAmount;
        var totalBefore = invoice.TotalAmount;
        Arrange(invoice);
        var handler = new UpdateInvoiceAmounts.Handler(_invoiceRepository.Object);

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

    // ── RejectInvoice (AC2) ──────────────────────────────────────────

    [Fact]
    public async Task RejectInvoice_Moves_Pending_To_Rejected_With_Notes()
    {
        var invoice = PendingInvoice();
        Arrange(invoice);
        var handler = new RejectInvoice.Handler(_invoiceRepository.Object);

        var result = await handler.Handle(
            new RejectInvoice.Command(InvoiceId, AdminNotes: "not eligible"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeInvoiceStatus.Rejected, invoice.Status);
        Assert.Equal("not eligible", invoice.AdminNotes);
    }

    [Fact]
    public async Task RejectInvoice_On_Paid_Invoice_Returns_Error_And_Does_Not_Mutate()
    {
        var invoice = PaidInvoice();
        Arrange(invoice);
        var handler = new RejectInvoice.Handler(_invoiceRepository.Object);

        var result = await handler.Handle(
            new RejectInvoice.Command(InvoiceId, AdminNotes: "x"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvoiceAlreadyPaid, result.Error!.Message);
        Assert.Equal(EmployeeInvoiceStatus.Paid, invoice.Status);
    }
}
