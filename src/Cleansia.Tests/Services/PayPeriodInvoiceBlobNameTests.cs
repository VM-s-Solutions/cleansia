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
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Cleansia.TestUtilities.MockDataFactories.Languages;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The address the auto-close job uploads a cleaner's invoice PDF to must be unique by construction.
/// Since each operating company numbers its own invoices, two companies' first invoices of a year are
/// both <c>INV-YYYY-000001</c>, the auto-seeded monthly periods share a label, and two cleaners can
/// share a display name — so a path keyed on the name collides across companies, and the second
/// upload either fails or replaces the first company's document under the URL its row already holds.
/// The employee id is the one segment that cannot coincide, and it is the segment the regenerate path
/// already keys on, so the two writers address the same file.
/// </summary>
public class PayPeriodInvoiceBlobNameTests
{
    private const string PdfUrl = "https://blobs.test/generated-invoices/invoice.pdf";
    private const string FirstCompanyEmployeeId = "emp-cz";
    private const string SecondCompanyEmployeeId = "emp-sk";

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

    private readonly Dictionary<string, PayPeriod> _expiredPeriodsByTenant = new()
    {
        [TestTenants.Default] = ExpiredPeriod("period-cz", TestTenants.Default),
        [TestTenants.Second] = ExpiredPeriod("period-sk", TestTenants.Second),
    };

    private readonly Dictionary<string, Employee> _cleanersByTenant = new()
    {
        [TestTenants.Default] = Cleaner(FirstCompanyEmployeeId),
        [TestTenants.Second] = Cleaner(SecondCompanyEmployeeId),
    };

    private readonly List<string> _uploadedBlobNames = [];
    private string? _currentTenant;

    public PayPeriodInvoiceBlobNameTests()
    {
        _tenantProvider
            .Setup(t => t.SetTenantOverride(It.IsAny<string>()))
            .Callback<string>(tenantId => _currentTenant = tenantId);
        _tenantProvider
            .Setup(t => t.ClearTenantOverride())
            .Callback(() => _currentTenant = null);

        _payPeriodRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(_expiredPeriodsByTenant.Values.AsQueryable().BuildMock());
        _payPeriodRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { PayrollMockFactory.OpenPeriod() }.AsQueryable().BuildMock());

        // Evaluated per read: the sweep adopts one company at a time, and the filtered employee read
        // answers for whichever company it has adopted.
        _employeeRepository
            .Setup(r => r.GetQueryable())
            .Returns(() => new[] { _cleanersByTenant[_currentTenant!] }.AsQueryable().BuildMock());

        _invoiceRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<EmployeeInvoice>().AsQueryable().BuildMock());
        _invoiceRepository
            .Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(invoice =>
                typeof(EmployeeInvoice)
                    .GetProperty(nameof(EmployeeInvoice.PayPeriod))!
                    .SetValue(invoice, _expiredPeriodsByTenant.Values.Single(p => p.Id == invoice.PayPeriodId)));

        _orderPayRepository
            .Setup(r => r.GetUnassignedForEmployeePeriodAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string employeeId, string payPeriodId, CancellationToken _) =>
                (IReadOnlyList<OrderEmployeePay>)[PayrollMockFactory.OrderPay(basePay: 100m, employeeId: employeeId, payPeriodId: payPeriodId)]);

        var currency = CurrencyMockFactory.Generate();
        currency.Id = PayrollMockFactory.CurrencyId;
        _currencyRepository
            .Setup(r => r.GetByIdAsync(PayrollMockFactory.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        // Each company's first references of the year: the same strings, by construction.
        _payoutReferenceAllocator
            .Setup(a => a.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(PayrollMockFactory.TestVariableSymbol));
        _payoutReferenceAllocator
            .Setup(a => a.AllocateInvoiceNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(PayrollMockFactory.TestInvoiceNumber));

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
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, Metadata?, CancellationToken>((name, _, _, _) => _uploadedBlobNames.Add(name))
            .Returns(Task.CompletedTask);
        _blobContainerClient
            .Setup(c => c.GetBlobUri(It.IsAny<string>()))
            .Returns(new Uri(PdfUrl));
    }

    [Fact]
    public async Task Two_Companies_Same_Name_Same_Period_Label_Same_Invoice_Number_Upload_To_Distinct_Blobs()
    {
        await Run();

        Assert.Equal(2, _uploadedBlobNames.Count);
        Assert.Equal(2, _uploadedBlobNames.Distinct(StringComparer.Ordinal).Count());

        Assert.Contains(_uploadedBlobNames, n => n.Contains($"/{FirstCompanyEmployeeId}/", StringComparison.Ordinal));
        Assert.Contains(_uploadedBlobNames, n => n.Contains($"/{SecondCompanyEmployeeId}/", StringComparison.Ordinal));
    }

    // ── arrangement ──────────────────────────────────────────────────

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
        _payoutReferenceAllocator.Object,
        new Mock<ITenantRepository>().Object)
        .CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);

    private static PayPeriod ExpiredPeriod(string id, string tenantId)
    {
        var period = PayrollMockFactory.OpenPeriod();
        period.Id = id;
        period.TenantId = tenantId;
        return period;
    }

    private static Employee Cleaner(string id)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Password1", "Jan", "Novák");
        user.UpdatePhoneNumber("+420777123456");

        var address = Address.Create("Dlouhá 12", "Praha", "11000", "cz");
        typeof(Address)
            .GetProperty(nameof(Address.Country))!
            .SetValue(address, Country.Create("Czechia", "CZE", "CZ"));

        var employee = Employee.CreateWithUser(user);
        employee.Id = id;
        employee.UpdateAddress(address);
        employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, "12345678", null);
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
            iban: "CZ1101000000001234567890",
            bankAccountNumber: "1234567890/0100",
            swift: "KOMBCZPP");
}
