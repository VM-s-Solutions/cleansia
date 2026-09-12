using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Approval is the admin's commitment to transfer, and the transfer itself is keyed by hand in a bank
/// outside the platform -- so it is the last point where the platform can refuse an invoice whose
/// currency the cleaner's account does not hold (T-0708). The account's currency is DECLARED by the
/// cleaner (<c>EmployeePayoutDetails.CurrencyId</c>); an undeclared account is taken to hold the
/// platform default, the assumption every destination was collected under. A missing payout record is
/// ADR-0034 D7's presence gate, not this rule's, and passes here.
/// </summary>
public class ApproveInvoicePayoutCurrencyTests
{
    private const string InvoiceId = "invoice-1";
    private const string EmployeeId = "emp-1";
    private const string AdminEmail = "admin@cleansia.cz";
    private const string CzkId = "currency-czk";
    private const string EurId = "currency-eur";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IEmployeePayoutDetailsRepository> _payoutDetails = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();

    public ApproveInvoicePayoutCurrencyTests()
    {
        var admin = User.CreateWithPassword(AdminEmail, "Password1", "Ad", "Min");
        admin.ConfirmEmail();
        _session.Setup(s => s.GetUserEmail()).Returns(AdminEmail);
        _users.Setup(r => r.GetByEmailAsync(AdminEmail, It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = CzkId;
        _currencies.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(czk);
    }

    [Fact]
    public async Task A_Declared_Account_Currency_That_Differs_From_The_Invoice_Refuses_Approval()
    {
        ArrangeInvoice(CzkId, EmployeeInvoiceStatus.Pending);
        ArrangePayout(currencyId: EurId);

        var result = await Validator().ValidateAsync(new ApproveInvoice.Command(InvoiceId, null));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.InvoicePayoutCurrencyMismatch, error.ErrorMessage);
        // A declared currency needs no default lookup.
        _currencies.Verify(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Declared_Account_Currency_That_Matches_The_Invoice_Approves()
    {
        ArrangeInvoice(CzkId, EmployeeInvoiceStatus.Pending);
        ArrangePayout(currencyId: CzkId);

        var result = await Validator().ValidateAsync(new ApproveInvoice.Command(InvoiceId, null));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(CzkId, true)]
    [InlineData(EurId, false)]
    public async Task An_Undeclared_Account_Is_Taken_To_Hold_The_Platform_Default(string invoiceCurrencyId, bool approves)
    {
        ArrangeInvoice(invoiceCurrencyId, EmployeeInvoiceStatus.Pending);
        ArrangePayout(currencyId: null);

        var result = await Validator().ValidateAsync(new ApproveInvoice.Command(InvoiceId, null));

        Assert.Equal(approves, result.IsValid);
        if (!approves)
        {
            Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvoicePayoutCurrencyMismatch);
        }
    }

    [Fact]
    public async Task A_Missing_Payout_Record_Is_Not_This_Rules_Concern()
    {
        ArrangeInvoice(EurId, EmployeeInvoiceStatus.Pending);
        _payoutDetails
            .Setup(r => r.GetByEmployeeIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeePayoutDetails?)null);

        var result = await Validator().ValidateAsync(new ApproveInvoice.Command(InvoiceId, null));

        Assert.True(result.IsValid);
        _currencies.Verify(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Non_Pending_Invoice_Is_Refused_For_Its_Status_Before_Its_Currency_Is_Looked_At()
    {
        ArrangeInvoice(CzkId, EmployeeInvoiceStatus.Approved);
        ArrangePayout(currencyId: EurId);

        var result = await Validator().ValidateAsync(new ApproveInvoice.Command(InvoiceId, null));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.InvalidInvoiceStatus, error.ErrorMessage);
        _payoutDetails.Verify(r => r.GetByEmployeeIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private ApproveInvoice.Validator Validator() => new(
        _users.Object, _session.Object, _invoiceRepository.Object, _payoutDetails.Object, _currencies.Object);

    private void ArrangeInvoice(string currencyId, EmployeeInvoiceStatus status)
    {
        var invoice = EmployeeInvoice.Create(
            employeeId: EmployeeId,
            payPeriodId: "period-1",
            totalOrders: 2,
            subTotal: 900m,
            currencyId: currencyId,
            variableSymbol: PayrollMockFactory.TestVariableSymbol);
        invoice.Id = InvoiceId;
        if (status == EmployeeInvoiceStatus.Approved)
        {
            invoice.Approve("admin-1");
        }
        Assert.Equal(status, invoice.Status);

        _invoiceRepository.Setup(r => r.ExistsAsync(InvoiceId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _invoiceRepository.Setup(r => r.GetByIdAsync(InvoiceId, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
    }

    private void ArrangePayout(string? currencyId)
    {
        var payout = EmployeePayoutDetails.Create(
            EmployeeId,
            PayoutScheme.CzskDomesticWithIban,
            "country-cz",
            PayoutDetailsStatus.Provided,
            iban: "CZ3155000000005885638003",
            currencyId: currencyId);
        _payoutDetails
            .Setup(r => r.GetByEmployeeIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payout);
    }
}
