using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The one-time assignment for invoices that predate payout references (ADR-0046 §D4.3). The gate is
/// the whole safety argument, so each arm is pinned separately: a PAID invoice can never receive a
/// first symbol (the owner's bank record references nothing, and the platform would claim a reference
/// that was never on the transfer), a cancelled one cannot either, and an invoice that already carries
/// one is never re-stamped.
/// </summary>
public class AssignInvoiceVariableSymbolTests
{
    private const string InvoiceId = "invoice-1";
    private const string LanguageCode = "en";

    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<IPayoutReferenceAllocator> _allocator = new();

    public AssignInvoiceVariableSymbolTests()
    {
        _invoiceRepository
            .Setup(r => r.ExistsAsync(InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _languageRepository
            .Setup(r => r.ExistsWithCodeAsync(LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _allocator
            .Setup(a => a.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success("2026000042"));
    }

    private AssignInvoiceVariableSymbol.Validator CreateValidator() =>
        new(_invoiceRepository.Object, _languageRepository.Object);

    private static AssignInvoiceVariableSymbol.Command Command() => new(InvoiceId, LanguageCode);

    private void Arrange(EmployeeInvoice invoice)
    {
        invoice.Id = InvoiceId;
        _invoiceRepository
            .Setup(r => r.GetByIdAsync(InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
    }

    private static EmployeeInvoice SymbolLess(EmployeeInvoiceStatus status = EmployeeInvoiceStatus.Pending)
    {
        var invoice = PayrollMockFactory.Invoice();

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

        // The legacy population: a row created before the allocator existed. It cannot be built through
        // Create any more, because the parameter is required.
        typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.VariableSymbol))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(invoice, [null]);

        return invoice;
    }

    [Fact]
    public async Task A_Paid_Invoice_Is_Refused_A_First_Variable_Symbol()
    {
        Arrange(SymbolLess(EmployeeInvoiceStatus.Paid));

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.InvoiceAlreadyPaid, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Cancelled_Invoice_Is_Refused()
    {
        Arrange(SymbolLess(EmployeeInvoiceStatus.Cancelled));

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.InvoiceAlreadyCancelled, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Rejected_Invoice_Is_Refused_Because_It_Is_Outside_The_Assignable_Statuses()
    {
        Arrange(SymbolLess(EmployeeInvoiceStatus.Rejected));

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.InvalidInvoiceStatus, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task An_Invoice_That_Already_Carries_A_Symbol_Is_Never_Re_Stamped()
    {
        Arrange(PayrollMockFactory.Invoice());

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.False(result.IsValid);
        Assert.Equal(
            BusinessErrorMessage.InvoiceReferenceAlreadyAssigned,
            Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(EmployeeInvoiceStatus.Pending)]
    [InlineData(EmployeeInvoiceStatus.Approved)]
    [InlineData(EmployeeInvoiceStatus.Disputed)]
    public async Task An_Unpaid_Uncancelled_Symbol_Less_Invoice_Passes_The_Gate(EmployeeInvoiceStatus status)
    {
        Arrange(SymbolLess(status));

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_Unknown_Invoice_Fails_Not_Found_Rather_Than_Dereferencing_Null()
    {
        _invoiceRepository
            .Setup(r => r.ExistsAsync(InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.InvoiceNotFound, Assert.Single(result.Errors).ErrorMessage);
    }
}
