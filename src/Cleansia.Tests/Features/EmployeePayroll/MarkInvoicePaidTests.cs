using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The owner pays each cleaner by manual bank transfer, so nothing but a person can tell the platform
/// the money moved. These pin what that person is allowed to assert: only an approved invoice is
/// markable, every other lifecycle state is refused with a code that says what to do next, and a
/// second mark is refused rather than silently overwriting the first actor's record.
/// </summary>
public class MarkInvoicePaidTests
{
    private const string InvoiceId = "invoice-1";

    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();

    private static EmployeeInvoice InvoiceIn(EmployeeInvoiceStatus status)
    {
        var invoice = EmployeeInvoice.Create(
            employeeId: "emp-1",
            payPeriodId: "period-1",
            totalOrders: 2,
            subTotal: 900m,
            currencyId: "currency-1");
        invoice.Id = InvoiceId;

        switch (status)
        {
            case EmployeeInvoiceStatus.Pending:
                break;
            case EmployeeInvoiceStatus.Approved:
                invoice.Approve("admin-1");
                break;
            case EmployeeInvoiceStatus.Paid:
                invoice.Approve("admin-1");
                invoice.MarkAsPaid();
                break;
            case EmployeeInvoiceStatus.Disputed:
                invoice.Dispute("contested");
                break;
            case EmployeeInvoiceStatus.Rejected:
                invoice.Reject("refused");
                break;
            case EmployeeInvoiceStatus.Cancelled:
                invoice.Cancel("void", "admin-1");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unhandled invoice status");
        }

        Assert.Equal(status, invoice.Status);
        return invoice;
    }

    private void Arrange(EmployeeInvoice invoice)
    {
        _invoiceRepository
            .Setup(r => r.GetByIdAsync(InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        _invoiceRepository
            .Setup(r => r.ExistsAsync(InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private MarkInvoicePaid.Handler CreateHandler() =>
        new(_invoiceRepository.Object, _notificationProducer.Object);

    private MarkInvoicePaid.Validator CreateValidator() => new(_invoiceRepository.Object);

    private static MarkInvoicePaid.Command Command() => new(InvoiceId, "VS 0001000001", null);

    /// <summary>The one code each lifecycle state must produce. <c>null</c> = the mark is allowed.</summary>
    public static TheoryData<EmployeeInvoiceStatus, string?> RefusalPerStatus() => new()
    {
        { EmployeeInvoiceStatus.Pending, BusinessErrorMessage.InvoiceNotApproved },
        { EmployeeInvoiceStatus.Approved, null },
        { EmployeeInvoiceStatus.Paid, BusinessErrorMessage.InvoiceAlreadyPaid },
        { EmployeeInvoiceStatus.Disputed, BusinessErrorMessage.InvoiceNotApproved },
        { EmployeeInvoiceStatus.Rejected, BusinessErrorMessage.InvoiceNotApproved },
        { EmployeeInvoiceStatus.Cancelled, BusinessErrorMessage.InvoiceAlreadyCancelled },
    };

    [Fact]
    public async Task Marking_An_Approved_Invoice_Stamps_The_Paid_State_And_The_Paid_Date()
    {
        var invoice = InvoiceIn(EmployeeInvoiceStatus.Approved);
        Arrange(invoice);
        var before = DateTime.UtcNow;

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeInvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAt);
        Assert.InRange(invoice.PaidAt!.Value, before, DateTime.UtcNow);
        Assert.Equal("VS 0001000001", invoice.BankTransferNote);
    }

    [Theory]
    [MemberData(nameof(RefusalPerStatus))]
    public async Task The_Validator_Refuses_Each_Unmarkable_State_With_Its_Own_Code(
        EmployeeInvoiceStatus status, string? expectedRefusal)
    {
        Arrange(InvoiceIn(status));

        var result = await CreateValidator().ValidateAsync(Command());

        if (expectedRefusal is null)
        {
            Assert.True(result.IsValid);
            return;
        }

        Assert.False(result.IsValid);
        Assert.Equal(expectedRefusal, Assert.Single(result.Errors).ErrorMessage);
    }

    /// <summary>
    /// The validator's status read and the handler's write are separate steps, so both must reach the
    /// same verdict for the same state — otherwise a state refused by one is accepted by the other the
    /// moment two admins act at once. Drives both production paths over one fixture per state.
    /// </summary>
    [Theory]
    [MemberData(nameof(RefusalPerStatus))]
    public async Task The_Handler_Refuses_Exactly_What_The_Validator_Refuses(
        EmployeeInvoiceStatus status, string? expectedRefusal)
    {
        Arrange(InvoiceIn(status));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        if (expectedRefusal is null)
        {
            Assert.True(result.IsSuccess);
            return;
        }

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedRefusal, result.Error!.Message);
        Assert.Equal(nameof(MarkInvoicePaid.Command.InvoiceId), result.Error.Code);
    }

    /// <summary>
    /// Marking is not idempotent by design: it is a person asserting a bank transfer went out, and a
    /// second assertion about the same variable symbol is a different transfer. Refusing surfaces the
    /// conflict; a silent no-op would hide a double payment, and re-stamping would erase who recorded
    /// the first one.
    /// </summary>
    [Fact]
    public async Task Marking_An_Already_Paid_Invoice_Again_Is_Refused_And_Leaves_The_First_Stamp_Intact()
    {
        var invoice = InvoiceIn(EmployeeInvoiceStatus.Paid);
        Arrange(invoice);
        var firstStamp = invoice.PaidAt;
        var firstNote = invoice.BankTransferNote;

        var result = await CreateHandler().Handle(
            new MarkInvoicePaid.Command(InvoiceId, "a second transfer", "paid again"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.InvoiceAlreadyPaid, result.Error!.Message);
        Assert.Equal(firstStamp, invoice.PaidAt);
        Assert.Equal(firstNote, invoice.BankTransferNote);
        _notificationProducer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task An_Unknown_Invoice_Fails_Not_Found()
    {
        _invoiceRepository
            .Setup(r => r.ExistsAsync(InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.InvoiceNotFound, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_Empty_InvoiceId_Fails_Required(string invoiceId)
    {
        var result = await CreateValidator().ValidateAsync(Command() with { InvoiceId = invoiceId });

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.Required, Assert.Single(result.Errors).ErrorMessage);
    }
}
