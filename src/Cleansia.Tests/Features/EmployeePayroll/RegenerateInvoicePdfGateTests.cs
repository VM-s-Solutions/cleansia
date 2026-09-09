using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// A SETTLED PAYOUT INVOICE IS NOT RE-RENDERED. Owner ruling 2026-09-09.
///
/// <para><b>Why it needed a gate at all.</b> The validator checked that the invoice existed and that
/// the language existed, and nothing else — while the handler OVERWRITES THE PDF IN PLACE, at the URL
/// the cleaner already holds. The money cannot move (amounts and currency are frozen on the row, and
/// the VAT posture is fixed by ruling), but the LANGUAGE can: re-rendering a paid invoice in another
/// language replaces the document a cleaner has already filed with their tax return.
/// <c>InvoiceDocumentLanguageTests</c> states in its own words that a copy in another language is a
/// SECOND document — this is what stops the first one being destroyed to make it.</para>
///
/// <para>Three routes reach this command: the admin host, the partner host, and an in-process dispatch
/// from <c>AssignInvoiceVariableSymbol</c>. That third one already refuses paid and cancelled invoices
/// on its own account, so the gate costs it nothing.</para>
/// </summary>
public class RegenerateInvoicePdfGateTests
{
    private const string InvoiceId = "invoice-1";
    private const string LanguageCode = "en";

    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();

    public RegenerateInvoicePdfGateTests()
    {
        _invoiceRepository
            .Setup(r => r.ExistsAsync(InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _languageRepository
            .Setup(r => r.ExistsWithCodeAsync(LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private RegenerateInvoicePdf.Validator CreateValidator() =>
        new(_invoiceRepository.Object, _languageRepository.Object);

    private void Arrange(EmployeeInvoiceStatus status)
    {
        var invoice = PayrollMockFactory.Invoice();
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
            case EmployeeInvoiceStatus.Cancelled:
                invoice.Cancel("void", "admin-1");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unhandled invoice status");
        }

        _invoiceRepository
            .Setup(r => r.GetByIdAsync(InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
    }

    private async Task<FluentValidation.Results.ValidationResult> ValidateAsync(EmployeeInvoiceStatus status)
    {
        Arrange(status);
        return await CreateValidator().ValidateAsync(new RegenerateInvoicePdf.Command(InvoiceId, LanguageCode));
    }

    [Fact]
    public async Task A_Paid_Invoice_Is_Not_Re_Rendered()
    {
        var result = await ValidateAsync(EmployeeInvoiceStatus.Paid);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvoiceAlreadyPaid);
    }

    [Fact]
    public async Task A_Cancelled_Invoice_Is_Not_Re_Rendered()
    {
        var result = await ValidateAsync(EmployeeInvoiceStatus.Cancelled);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvoiceAlreadyCancelled);
    }

    /// <summary>
    /// ANTI-VACUITY, and the reason the gate is on settlement rather than on "has been rendered". A
    /// re-render is the ordinary way an admin fixes a failed render or issues the document in the
    /// cleaner's own language, and both happen before the invoice is paid. A gate that refused
    /// everything would pass the two tests above and break the feature.
    /// </summary>
    [Theory]
    [InlineData(EmployeeInvoiceStatus.Pending)]
    [InlineData(EmployeeInvoiceStatus.Approved)]
    public async Task An_Unsettled_Invoice_Is_Still_Re_Renderable(EmployeeInvoiceStatus status)
    {
        var result = await ValidateAsync(status);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }
}
