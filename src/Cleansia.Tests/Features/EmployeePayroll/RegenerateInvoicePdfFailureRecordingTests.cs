using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Cleansia.TestUtilities.MockDataFactories.Languages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// ADR-0046 §D4.3 rests on a re-runnable regenerate: allocate → stamp → commit → render, so a render
/// that fails leaves the invoice holding its number and an admin re-runs the render. That only works
/// if the ROW remembers the failure — the response is transient and the log line is not a state. Each
/// failure mode of the render path is therefore pinned on the persisted flag, and every arrangement
/// starts from a HEALTHY invoice (a stored URL, no recorded failure) so the assertion is a transition
/// rather than a fixture default nobody set.
/// </summary>
public class RegenerateInvoicePdfFailureRecordingTests
{
    private const string LanguageCode = "en";
    private const string PreviousPdfUrl = "https://blobs.test/generated-invoices/previous.pdf";
    private const string FreshPdfUrl = "https://blobs.test/generated-invoices/fresh.pdf";

    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<IBlobContainerClientFactory> _blobContainerClientFactory = new();
    private readonly Mock<IBlobContainerClient> _blobContainerClient = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IEmployeePayoutDetailsRepository> _payoutDetailsRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<ICountryInvoiceConfigRepository> _countryInvoiceConfigRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();

    private readonly EmployeeInvoice _invoice;

    public RegenerateInvoicePdfFailureRecordingTests()
    {
        _invoice = PayrollMockFactory.Invoice(payPeriod: PayrollMockFactory.OpenPeriod());

        // The state the defect hides in: a healthy row carrying a document from an earlier render.
        _invoice.SetPdfBlobUrl(PreviousPdfUrl);
        _invoice.ClearPdfGenerationError();

        _invoiceRepository
            .Setup(r => r.GetByIdAsync(_invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_invoice);
        _employeeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cleaner());
        _currencyRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CurrencyMockFactory.Generate());
        _companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company());
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company());
        _orderPayRepository
            .Setup(r => r.GetByInvoiceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PayrollMockFactory.OrderPay(basePay: 100m)]);
        _languageRepository
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LanguageMockFactory.Generate());
        _pdfService
            .Setup(s => s.GenerateInvoicePdf(
                It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Returns([1, 2, 3]);
        _blobContainerClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_blobContainerClient.Object);
        _blobContainerClient
            .Setup(c => c.GetBlobUri(It.IsAny<string>()))
            .Returns(new Uri(FreshPdfUrl));
    }

    // ── the failure modes, one per test, all asserted on the ROW ──────

    [Fact]
    public async Task A_Renderer_That_Throws_Is_Recorded_On_The_Row()
    {
        _pdfService
            .Setup(s => s.GenerateInvoicePdf(
                It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Throws(new InvalidOperationException("layout blew up"));

        var result = await Handle();

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PdfGenerationFailed, result.Error!.Message);
        AssertFailureIsPersisted("layout blew up");
    }

    [Fact]
    public async Task An_Empty_Render_Is_Recorded_Rather_Than_Stored()
    {
        _pdfService
            .Setup(s => s.GenerateInvoicePdf(
                It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Returns([]);

        var result = await Handle();

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PdfGenerationFailed, result.Error!.Message);
        AssertFailureIsPersisted();

        // A zero-byte upload would overwrite the readable document already at that blob name with an
        // unopenable one, and the row would go on claiming a URL that resolves to nothing.
        _blobContainerClient.Verify(
            c => c.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task An_Upload_That_Throws_Is_Recorded_On_The_Row()
    {
        _blobContainerClient
            .Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("blob storage unreachable"));

        var result = await Handle();

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PdfGenerationFailed, result.Error!.Message);
        AssertFailureIsPersisted("blob storage unreachable");
    }

    [Fact]
    public async Task A_Missing_Company_Info_Is_Recorded_On_The_Row()
    {
        _companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyInfo?)null);
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyInfo?)null);

        var result = await Handle();

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CompanyInfoNotFound, result.Error!.Message);
        AssertFailureIsPersisted();
    }

    [Fact]
    public async Task A_Read_That_Throws_Is_Recorded_On_The_Row()
    {
        _orderPayRepository
            .Setup(r => r.GetByInvoiceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("order pays read timed out"));

        var result = await Handle();

        Assert.True(result.IsFailure);
        AssertFailureIsPersisted("order pays read timed out");
    }

    // ── the recording must not become the new way the request explodes ─

    [Fact]
    public async Task A_Failure_Whose_Own_Flush_Fails_Still_Refuses_Instead_Of_Throwing()
    {
        _pdfService
            .Setup(s => s.GenerateInvoicePdf(
                It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Throws(new InvalidOperationException("layout blew up"));
        _invoiceRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("context is toast"));

        var result = await Handle();

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PdfGenerationFailed, result.Error!.Message);
    }

    // ── and the success path still clears a REAL prior failure ────────

    [Fact]
    public async Task A_Successful_Regenerate_Clears_A_Failure_That_Was_Actually_Recorded()
    {
        _invoice.SetPdfGenerationError("an earlier render failed");
        Assert.True(_invoice.PdfGenerationFailed);

        var result = await Handle();

        Assert.True(result.IsSuccess);
        Assert.Equal(FreshPdfUrl, result.Value!.PdfBlobUrl);
        Assert.Equal(FreshPdfUrl, _invoice.PdfBlobUrl);
        Assert.False(_invoice.PdfGenerationFailed);
        Assert.Null(_invoice.PdfGenerationError);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private void AssertFailureIsPersisted(string? expectedErrorFragment = null)
    {
        Assert.True(_invoice.PdfGenerationFailed);
        Assert.False(string.IsNullOrWhiteSpace(_invoice.PdfGenerationError));
        Assert.NotNull(_invoice.PdfGenerationAttemptedAt);

        if (expectedErrorFragment is not null)
        {
            Assert.Contains(expectedErrorFragment, _invoice.PdfGenerationError!, StringComparison.Ordinal);
        }

        // The pipeline commits successful commands only, so a failure state that rides it is dropped.
        _invoiceRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

        // The row must not go on advertising a document that does not carry what the row claims.
        Assert.Equal(PreviousPdfUrl, _invoice.PdfBlobUrl);
    }

    private Task<Infra.Common.Validations.BusinessResult<RegenerateInvoicePdf.Response>> Handle() =>
        new RegenerateInvoicePdf.Handler(
            _pdfService.Object,
            _currencyRepository.Object,
            _employeeRepository.Object,
            _languageRepository.Object,
            _companyInfoRepository.Object,
            _blobContainerClientFactory.Object,
            _invoiceRepository.Object,
            _payoutDetailsRepository.Object,
            _orderPayRepository.Object,
            _countryInvoiceConfigRepository.Object,
            _countryConfigurationRepository.Object,
            NullLogger<RegenerateInvoicePdf.Handler>.Instance)
            .Handle(new RegenerateInvoicePdf.Command(_invoice.Id, LanguageCode), CancellationToken.None);

    private static Employee Cleaner()
    {
        var user = User.CreateWithPassword("jan.novak@cleansia.test", "Password1", "Jan", "Novák");
        user.UpdatePhoneNumber("+420777123456");

        var address = Address.Create("Dlouhá 12", "Praha", "11000", "cz");
        typeof(Address)
            .GetProperty(nameof(Address.Country))!
            .SetValue(address, Country.Create("Czechia", "CZE"));

        var employee = Employee.CreateWithUser(user);
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
