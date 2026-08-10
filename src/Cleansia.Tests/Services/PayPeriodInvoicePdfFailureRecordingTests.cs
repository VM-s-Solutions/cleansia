using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Cleansia.TestUtilities.MockDataFactories.Languages;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The auto-close job renders each cleaner's invoice PDF, and the invoice ROW is the admin's only
/// durable signal that a render failed (ADR-0046 Erratum E5) — the whole "just re-run the regenerate"
/// remedy depends on knowing WHY. So every failure mode must reach
/// <c>EmployeeInvoice.PdfGenerationError</c> carrying its own cause: a placeholder standing in for the
/// genuine exception is indistinguishable, on the row, from an empty render, and sends an admin to a
/// server log they may not have.
///
/// Each assertion is on the persisted row rather than on a log line, and each arranges a distinctive
/// real cause so that a fixture default nobody set cannot satisfy it.
/// </summary>
public class PayPeriodInvoicePdfFailureRecordingTests
{
    private const string EmptyRenderMessage = "PDF generation returned empty result";
    private const string PdfUrl = "https://blobs.test/generated-invoices/invoice.pdf";
    private const int PdfGenerationErrorColumnWidth = 1000;

    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IEmployeePayoutDetailsRepository> _payoutDetailsRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICountryInvoiceConfigRepository> _countryInvoiceConfigRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<IBlobContainerClientFactory> _blobContainerClientFactory = new();
    private readonly Mock<IBlobContainerClient> _blobContainerClient = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IPayoutReferenceAllocator> _payoutReferenceAllocator = new();

    private readonly PayPeriod _expiredPeriod = PayrollMockFactory.OpenPeriod();

    private EmployeeInvoice? _addedInvoice;

    public PayPeriodInvoicePdfFailureRecordingTests()
    {
        _payPeriodRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { _expiredPeriod }.AsQueryable().BuildMock());
        _payPeriodRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { PayrollMockFactory.OpenPeriod() }.AsQueryable().BuildMock());

        _employeeRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { Cleaner() }.AsQueryable().BuildMock());

        _invoiceRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<EmployeeInvoice>().AsQueryable().BuildMock());
        _invoiceRepository
            .Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(invoice =>
            {
                _addedInvoice = invoice;

                // EF links the PayPeriod navigation by relationship fixup on commit; mocked
                // repositories do not, and both the renderer and the blob name dereference it.
                typeof(EmployeeInvoice)
                    .GetProperty(nameof(EmployeeInvoice.PayPeriod))!
                    .SetValue(invoice, _expiredPeriod);
            });

        _orderPayRepository
            .Setup(r => r.GetUnassignedForEmployeePeriodAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PayrollMockFactory.OrderPay(basePay: 100m)]);

        var currency = CurrencyMockFactory.Generate();
        currency.Id = PayrollMockFactory.CurrencyId;
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        _payoutReferenceAllocator
            .Setup(a => a.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(PayrollMockFactory.TestVariableSymbol));

        _languageRepository
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LanguageMockFactory.Generate());

        _companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company());
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company());

        _pdfService
            .Setup(s => s.GenerateInvoicePdf(
                It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Returns([1, 2, 3]);

        _blobContainerClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_blobContainerClient.Object);
        _blobContainerClient
            .Setup(c => c.GetBlobUri(It.IsAny<string>()))
            .Returns(new Uri(PdfUrl));
    }

    // ── anti-vacuity: the run really does reach the render ────────────

    [Fact]
    public async Task A_Successful_Render_Stores_The_Document_And_Records_No_Failure()
    {
        await Run();

        Assert.NotNull(_addedInvoice);
        Assert.Equal(PdfUrl, _addedInvoice!.PdfBlobUrl);
        Assert.False(_addedInvoice.PdfGenerationFailed);
        Assert.Null(_addedInvoice.PdfGenerationError);
    }

    // ── the failure modes, one per test, all asserted on the ROW ──────

    [Fact]
    public async Task A_Renderer_That_Throws_Records_Its_Own_Cause_Not_The_Empty_Render_Placeholder()
    {
        _pdfService
            .Setup(s => s.GenerateInvoicePdf(
                It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Throws(new InvalidOperationException("QuestPDF ran out of space on page 3"));

        await Run();

        AssertFailureIsPersisted("QuestPDF ran out of space on page 3");
        Assert.DoesNotContain(EmptyRenderMessage, _addedInvoice!.PdfGenerationError!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Read_That_Throws_Records_Its_Own_Cause_Not_The_Empty_Render_Placeholder()
    {
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("country configuration read timed out"));

        await Run();

        AssertFailureIsPersisted("country configuration read timed out");
        Assert.DoesNotContain(EmptyRenderMessage, _addedInvoice!.PdfGenerationError!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Missing_Company_Info_Records_That_It_Is_Missing_Not_The_Empty_Render_Placeholder()
    {
        _companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyInfo?)null);
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyInfo?)null);

        await Run();

        AssertFailureIsPersisted("company info");
        Assert.DoesNotContain(EmptyRenderMessage, _addedInvoice!.PdfGenerationError!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Upload_That_Throws_Records_Its_Own_Cause()
    {
        _blobContainerClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("blob storage unreachable"));

        await Run();

        AssertFailureIsPersisted("blob storage unreachable");
    }

    // An empty render is the ONE case the placeholder actually describes, and it has to keep saying so
    // — otherwise the fix above trades a wrong message for a missing one.
    [Fact]
    public async Task An_Empty_Render_Is_Still_Recorded_As_An_Empty_Render()
    {
        _pdfService
            .Setup(s => s.GenerateInvoicePdf(
                It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Returns([]);

        await Run();

        AssertFailureIsPersisted(EmptyRenderMessage);
    }

    // The cause is an exception's message, so its length is whatever some driver or layout engine felt
    // like emitting. Recording it only became this path's problem once the REAL message started
    // arriving here; the truncation lives on the entity setter, so this path inherits it.
    [Fact]
    public async Task An_Over_Long_Cause_Is_Truncated_To_The_Column_Width_Rather_Than_Failing_The_Write()
    {
        var overLong = new string('x', PdfGenerationErrorColumnWidth * 5);
        _pdfService
            .Setup(s => s.GenerateInvoicePdf(
                It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Throws(new InvalidOperationException(overLong));

        await Run();

        Assert.NotNull(_addedInvoice);
        Assert.True(_addedInvoice!.PdfGenerationFailed);
        Assert.Equal(PdfGenerationErrorColumnWidth, _addedInvoice.PdfGenerationError!.Length);
        Assert.Equal(overLong[..PdfGenerationErrorColumnWidth], _addedInvoice.PdfGenerationError);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private void AssertFailureIsPersisted(string expectedErrorFragment)
    {
        Assert.NotNull(_addedInvoice);
        Assert.True(_addedInvoice!.PdfGenerationFailed);
        Assert.NotNull(_addedInvoice.PdfGenerationError);
        Assert.Contains(expectedErrorFragment, _addedInvoice.PdfGenerationError!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(_addedInvoice.PdfGenerationAttemptedAt);

        // The row must not advertise a document the failed render never produced.
        Assert.Null(_addedInvoice.PdfBlobUrl);
    }

    private Task Run() => new PayPeriodBackgroundService(
        _payPeriodRepository.Object,
        _employeeRepository.Object,
        _emailService.Object,
        _unitOfWork.Object,
        NullLogger<PayPeriodBackgroundService>.Instance,
        _currencyRepository.Object,
        _invoiceRepository.Object,
        _payoutDetailsRepository.Object,
        _orderPayRepository.Object,
        _companyInfoRepository.Object,
        _languageRepository.Object,
        _countryInvoiceConfigRepository.Object,
        _countryConfigurationRepository.Object,
        _pdfService.Object,
        _blobContainerClientFactory.Object,
        _tenantProvider.Object,
        _payoutReferenceAllocator.Object)
        .CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);

    private static Employee Cleaner()
    {
        var user = User.CreateWithPassword("jan.novak@cleansia.test", "Password1", "Jan", "Novák");
        user.UpdatePhoneNumber("+420777123456");

        var address = Address.Create("Dlouhá 12", "Praha", "11000", "cz");
        typeof(Address)
            .GetProperty(nameof(Address.Country))!
            .SetValue(address, Country.Create("Czechia", "CZE"));

        var employee = Employee.CreateWithUser(user);
        employee.Id = PayrollMockFactory.EmployeeId;
        employee.UpdateAddress(address);
        employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, "12345678", null, null);
        return employee;
    }

    private static CompanyInfo Company() =>
        CompanyInfo.Create(
            legalName: "Cleansia s.r.o.",
            tradingName: "Cleansia",
            registrationNumber: "87654321",
            street: "Václavské náměstí 1",
            city: "Praha",
            zipCode: "11000",
            countryId: "cz",
            vatNumber: "CZ87654321",
            iban: "CZ1101000000001234567890",
            bankAccountNumber: "1234567890/0100",
            swift: "KOMBCZPP");
}
