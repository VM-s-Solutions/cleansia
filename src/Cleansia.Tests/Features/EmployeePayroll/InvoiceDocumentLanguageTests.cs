using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Cleansia.TestUtilities.MockDataFactories.Languages;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// A payout invoice prints in its JURISDICTION's language — the layout builder is selected from the
/// cleaner's country ISO code, and that is the only input that decides it on either render path. The
/// re-render must reproduce the original: an invoice and its regenerated copy carry the same number
/// and the same money, so a second copy in a different language is a second document.
///
/// <para><c>RegenerateInvoicePdf.Command</c> carries a <c>LanguageCode</c> that decides none of this.
/// It is still on the wire, so what these pin is that it stays inert — honouring it is what would
/// break the reproduction, and it would put the caller's language on a legal-notice box that is
/// reviewed per country (<c>InvoiceLabels.UnreviewedJurisdictionNotice</c>).</para>
/// </summary>
public class InvoiceDocumentLanguageTests
{
    private const string CleanerCountryIsoCode = "CZE";
    private const string ADifferentLanguage = "en";
    private const string AnotherDifferentLanguage = "cs";
    private const string PdfUrl = "https://blobs.test/generated-invoices/invoice.pdf";

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
    private readonly Employee _cleaner = Cleaner();
    private readonly EmployeeInvoice _existingInvoice =
        PayrollMockFactory.Invoice(payPeriod: PayrollMockFactory.OpenPeriod());

    private readonly List<string?> _renderedCountryCodes = [];

    public InvoiceDocumentLanguageTests()
    {
        _payPeriodRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { _expiredPeriod }.AsQueryable().BuildMock());
        _payPeriodRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { PayrollMockFactory.OpenPeriod() }.AsQueryable().BuildMock());

        _employeeRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { _cleaner }.AsQueryable().BuildMock());
        _employeeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cleaner);

        _invoiceRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<EmployeeInvoice>().AsQueryable().BuildMock());
        _invoiceRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_existingInvoice);
        _invoiceRepository
            .Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(invoice =>
                // EF links PayPeriod by relationship fixup on commit; mocked repositories do not, and
                // the renderer dereferences it.
                typeof(EmployeeInvoice)
                    .GetProperty(nameof(EmployeeInvoice.PayPeriod))!
                    .SetValue(invoice, _expiredPeriod));

        _orderPayRepository
            .Setup(r => r.GetUnassignedForEmployeePeriodAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PayrollMockFactory.OrderPay(basePay: 100m)]);
        _orderPayRepository
            .Setup(r => r.GetByInvoiceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PayrollMockFactory.OrderPay(basePay: 100m)]);

        var currency = CurrencyMockFactory.Generate();
        currency.Id = PayrollMockFactory.CurrencyId;
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        _currencyRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            .Callback((InvoicePdfData _, CountryInvoiceContext? _, string? countryCode) =>
                _renderedCountryCodes.Add(countryCode))
            .Returns([1, 2, 3]);

        _blobContainerClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_blobContainerClient.Object);
        _blobContainerClient
            .Setup(c => c.GetBlobUri(It.IsAny<string>()))
            .Returns(new Uri(PdfUrl));
    }

    [Fact]
    public async Task A_Re_Render_Prints_In_The_Same_Language_As_The_Original()
    {
        await RenderTheOriginal();
        var original = LastRender();

        var regenerated = await ReRender(ADifferentLanguage);
        Assert.True(regenerated.IsSuccess);

        Assert.Equal(SelectedLayout(original), SelectedLayout(LastRender()));

        // Anti-vacuity: both really did select the CZECH document, not "both fell through to the same
        // default because neither was given a country".
        Assert.IsType<CzechInvoiceLayoutBuilder>(SelectedLayout(LastRender()));
        Assert.Equal(CleanerCountryIsoCode, LastRender());
    }

    [Fact]
    public async Task The_Callers_LanguageCode_Does_Not_Steer_The_Re_Render()
    {
        Assert.True((await ReRender(ADifferentLanguage)).IsSuccess);
        var askedForOneLanguage = LastRender();

        Assert.True((await ReRender(AnotherDifferentLanguage)).IsSuccess);
        var askedForAnother = LastRender();

        Assert.Equal(askedForOneLanguage, askedForAnother);
        Assert.Equal(CleanerCountryIsoCode, askedForAnother);
        Assert.NotEqual(ADifferentLanguage, askedForAnother);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private string? LastRender()
    {
        Assert.NotEmpty(_renderedCountryCodes);
        return _renderedCountryCodes[^1];
    }

    // One factory over one registered builder set, as DI resolves it — so "the same document language"
    // is the same instance and not merely two equal-looking ones.
    private static readonly LayoutBuilderFactory Layouts =
        new([], [new DefaultInvoiceLayoutBuilder(), new CzechInvoiceLayoutBuilder()]);

    private static IInvoiceLayoutBuilder SelectedLayout(string? countryCode) =>
        Layouts.GetInvoiceBuilder(countryCode);

    private Task RenderTheOriginal() => new PayPeriodBackgroundService(
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

    private Task<BusinessResult<RegenerateInvoicePdf.Response>> ReRender(string languageCode) =>
        new RegenerateInvoicePdf.Handler(
            _pdfService.Object,
            _currencyRepository.Object,
            _employeeRepository.Object,
            _companyInfoRepository.Object,
            _blobContainerClientFactory.Object,
            _invoiceRepository.Object,
            _payoutDetailsRepository.Object,
            _orderPayRepository.Object,
            _countryInvoiceConfigRepository.Object,
            _countryConfigurationRepository.Object,
            NullLogger<RegenerateInvoicePdf.Handler>.Instance)
            .Handle(new RegenerateInvoicePdf.Command(_existingInvoice.Id, languageCode), CancellationToken.None);

    private static Employee Cleaner()
    {
        var user = User.CreateWithPassword("jan.novak@cleansia.test", "Password1", "Jan", "Novák");
        user.UpdatePhoneNumber("+420777123456");

        var address = Address.Create("Dlouhá 12", "Praha", "11000", "cz");
        typeof(Address)
            .GetProperty(nameof(Address.Country))!
            .SetValue(address, Country.Create("Czechia", CleanerCountryIsoCode));

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
