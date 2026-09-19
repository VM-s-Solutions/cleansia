using System.Reflection;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Constants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The row stores the absolute address the writers get back from the container, and the download
/// reads by name within it — two different strings for one blob. What is pinned here is the round
/// trip through the real writer and the real reader: the name the download hands the client is the
/// name the upload wrote, with the period label's spaces and comma surviving the address's escaping.
/// </summary>
public class InvoicePdfBlobNameRoundTripTests
{
    private const string ContainerAddress = "https://cleansiadev.blob.core.windows.net/" + Constants.BlobContainers.GeneratedInvoices;
    private const string LanguageCode = "en";

    private readonly Mock<IBlobContainerClient> _blobContainerClient = new();
    private readonly Mock<IBlobContainerClientFactory> _blobContainerClientFactory = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly EmployeeInvoice _invoice = PayrollMockFactory.Invoice(payPeriod: PayrollMockFactory.OpenPeriod());
    private readonly List<string> _uploadedNames = [];
    private readonly List<string> _downloadedNames = [];

    public InvoicePdfBlobNameRoundTripTests()
    {
        _blobContainerClientFactory
            .Setup(f => f.GetBlobContainerClient(Constants.BlobContainers.GeneratedInvoices))
            .Returns(_blobContainerClient.Object);
        _blobContainerClient
            .Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, Metadata?, CancellationToken>((name, _, _, _) => _uploadedNames.Add(name))
            .Returns(Task.CompletedTask);
        // The SDK escapes each path segment of the name into the address it returns.
        _blobContainerClient
            .Setup(c => c.GetBlobUri(It.IsAny<string>()))
            .Returns<string>(name => new Uri($"{ContainerAddress}/{string.Join('/', name.Split('/').Select(Uri.EscapeDataString))}"));
        _blobContainerClient
            .Setup(c => c.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => _downloadedNames.Add(name))
            .ReturnsAsync(() => new BlobFile(new MemoryStream([1, 2, 3]), "application/pdf"));
        _invoiceRepository
            .Setup(r => r.GetByIdAsync(_invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_invoice);
    }

    [Fact]
    public async Task The_Download_Reads_The_Name_The_Regenerate_Wrote()
    {
        var regenerated = await RegenerateAsync();
        Assert.True(regenerated.IsSuccess);
        var uploaded = Assert.Single(_uploadedNames);
        Assert.Contains(' ', uploaded);
        Assert.Contains(',', uploaded);
        Assert.StartsWith($"{ContainerAddress}/", _invoice.PdfBlobUrl, StringComparison.Ordinal);

        var downloaded = await DownloadAsync();

        Assert.True(downloaded.IsSuccess);
        Assert.Equal(uploaded, Assert.Single(_downloadedNames));
    }

    [Fact]
    public async Task A_Row_That_Already_Holds_A_Bare_Name_Is_Read_By_That_Name()
    {
        _invoice.SetPdfBlobUrl("Jan 01 - Jan 14, 2026/emp-1/INV-2026-000001.pdf");

        var downloaded = await DownloadAsync();

        Assert.True(downloaded.IsSuccess);
        Assert.Equal("Jan 01 - Jan 14, 2026/emp-1/INV-2026-000001.pdf", Assert.Single(_downloadedNames));
    }

    private Task<BusinessResult<RegenerateInvoicePdf.Response>> RegenerateAsync()
    {
        var pdfService = new Mock<IPdfService>();
        pdfService
            .Setup(s => s.GenerateInvoicePdf(It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Returns([1, 2, 3]);
        var currencyRepository = new Mock<ICurrencyRepository>();
        currencyRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CurrencyMockFactory.Generate());
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cleaner());
        var companyInfoRepository = new Mock<ICompanyInfoRepository>();
        companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company());
        companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company());
        var orderPayRepository = new Mock<IOrderEmployeePayRepository>();
        orderPayRepository
            .Setup(r => r.GetByInvoiceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PayrollMockFactory.OrderPay(basePay: 100m)]);

        return new RegenerateInvoicePdf.Handler(
            pdfService.Object,
            currencyRepository.Object,
            employeeRepository.Object,
            companyInfoRepository.Object,
            _blobContainerClientFactory.Object,
            _invoiceRepository.Object,
            new Mock<IEmployeePayoutDetailsRepository>().Object,
            orderPayRepository.Object,
            new Mock<ICountryInvoiceConfigRepository>().Object,
            new Mock<ICountryConfigurationRepository>().Object,
            NullLogger<RegenerateInvoicePdf.Handler>.Instance)
            .Handle(new RegenerateInvoicePdf.Command(_invoice.Id, LanguageCode), CancellationToken.None);
    }

    // DownloadInvoice.Handler is internal; resolve it via reflection, matching CreateAdminUserPasswordHashingTests.
    private async Task<BusinessResult<DownloadInvoice.Response>> DownloadAsync()
    {
        var handlerType = typeof(DownloadInvoice).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(
            handlerType!,
            _invoiceRepository.Object,
            new Mock<IOrderAccessService>().Object,
            new TestUserSessionProvider("admin-1", "admin@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]),
            _blobContainerClientFactory.Object)!;
        var handle = handlerType!.GetMethod("Handle");
        Assert.NotNull(handle);
        return await (Task<BusinessResult<DownloadInvoice.Response>>)handle!.Invoke(
            handler, [new DownloadInvoice.Query(_invoice.Id), CancellationToken.None])!;
    }

    private static Employee Cleaner()
    {
        var user = User.CreateWithPassword("jan.novak@cleansia.test", "Password1", "Jan", "Novák");
        user.UpdatePhoneNumber("+420777123456");

        var address = Address.Create("Dlouhá 12", "Praha", "11000", "cz");
        typeof(Address)
            .GetProperty(nameof(Address.Country))!
            .SetValue(address, Country.Create("Czechia", "CZE", "CZ"));

        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-1";
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
