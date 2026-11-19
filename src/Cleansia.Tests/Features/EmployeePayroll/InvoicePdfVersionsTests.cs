using System.Reflection;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Blobs.Abstractions;
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
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;
using Constants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// An issued invoice PDF is never overwritten: every render is a new file next to the first, the row
/// points at the newest, and the earlier files stay as issued (→ /decisions/adr-0083).
/// </summary>
public class InvoicePdfVersionsTests
{
    private readonly InMemoryBlobContainer _store = new(Constants.BlobContainers.GeneratedInvoices);
    private readonly Mock<IBlobContainerClientFactory> _factory = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoices = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<ICompanyInfoRepository> _companies = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPays = new();
    private readonly Mock<IPdfService> _pdf = new();
    private readonly EmployeeInvoice _invoice = PayrollMockFactory.Invoice(payPeriod: PayrollMockFactory.OpenPeriod());
    private readonly Employee _cleaner = Cleaner();
    private byte _renders;

    public InvoicePdfVersionsTests()
    {
        _factory.Setup(f => f.GetBlobContainerClient(Constants.BlobContainers.GeneratedInvoices)).Returns(_store);
        _invoices.Setup(r => r.GetAll()).Returns(() => new[] { _invoice }.AsQueryable().BuildMock());
        _invoices.Setup(r => r.GetByIdAsync(_invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_invoice);
        _employees.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_cleaner);
        _currencies.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CurrencyMockFactory.Generate());
        _companies.Setup(r => r.GetActiveByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Company());
        _orderPays.Setup(r => r.GetByInvoiceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PayrollMockFactory.OrderPay(basePay: 100m)]);
        _pdf.Setup(s => s.GenerateInvoicePdf(It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Returns(() => [++_renders]);
    }

    private string FirstName =>
        $"{PayPeriod.BlobFolder(_invoice.PayPeriod!.StartDate, _invoice.PayPeriod.EndDate)}/{_cleaner.Id}/{_invoice.InvoiceNumber}.pdf";

    private string SecondName => FirstName.Replace(".pdf", "-2.pdf");

    [Fact]
    public async Task Rendering_Twice_Keeps_Both_Files_And_Points_The_Row_At_The_Second()
    {
        Assert.True((await Regenerate()).IsSuccess);
        var first = _store.Blobs[FirstName];

        var second = await Regenerate();

        Assert.True(second.IsSuccess);
        Assert.Equal(2, _store.Blobs.Count);
        Assert.Equal(first, _store.Blobs[FirstName]);
        Assert.Equal([2], _store.Blobs[SecondName]);
        Assert.Equal(_store.GetBlobUri(SecondName).ToString(), _invoice.PdfBlobUrl);
        Assert.Equal(_invoice.PdfBlobUrl, second.Value!.PdfBlobUrl);
    }

    [Fact]
    public async Task A_Night_File_Whose_Stamp_Was_Lost_Is_Kept_And_The_Repair_Takes_The_Next_Number()
    {
        _store.Blobs[FirstName] = [9];
        Assert.Null(_invoice.PdfBlobUrl);

        var result = await Regenerate();

        Assert.True(result.IsSuccess);
        Assert.Equal([9], _store.Blobs[FirstName]);
        Assert.Equal(_store.GetBlobUri(SecondName).ToString(), _invoice.PdfBlobUrl);
        Assert.False(_invoice.PdfGenerationFailed);
    }

    [Fact]
    public async Task Adjusting_The_Amounts_Renders_A_New_File_Next_To_The_Night_One()
    {
        _store.Blobs[FirstName] = [9];
        _invoice.SetPdfBlobUrl(_store.GetBlobUri(FirstName).ToString());

        var result = await new UpdateInvoiceAmounts.Handler(
                _invoices.Object, new Mock<ICashLedgerRepository>().Object, ScopeRunningTheRealRender(),
                NullLogger<UpdateInvoiceAmounts.Handler>.Instance)
            .Handle(new UpdateInvoiceAmounts.Command(_invoice.Id, BonusAmount: 50m, DeductionAmount: 0m, AdminNotes: null),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([9], _store.Blobs[FirstName]);
        Assert.Equal(_store.GetBlobUri(SecondName).ToString(), _invoice.PdfBlobUrl);
    }

    [Fact]
    public async Task A_Download_Serves_The_Newest_Render()
    {
        await Regenerate();
        await Regenerate();

        var download = await Download();

        Assert.True(download.IsSuccess);
        Assert.Equal([2], download.Value!.PdfBytes);
    }

    private Task<BusinessResult<RegenerateInvoicePdf.Response>> Regenerate() =>
        RegenerateHandler().Handle(new RegenerateInvoicePdf.Command(_invoice.Id, "en"), CancellationToken.None);

    private RegenerateInvoicePdf.Handler RegenerateHandler() => new(
        _pdf.Object,
        _currencies.Object,
        _employees.Object,
        _companies.Object,
        _factory.Object,
        _invoices.Object,
        new Mock<IEmployeePayoutDetailsRepository>().Object,
        _orderPays.Object,
        new Mock<ICountryInvoiceConfigRepository>().Object,
        new Mock<ICountryConfigurationRepository>().Object,
        NullLogger<RegenerateInvoicePdf.Handler>.Instance);

    private IServiceScopeFactory ScopeRunningTheRealRender()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<RegenerateInvoicePdf.Command>(), It.IsAny<CancellationToken>()))
            .Returns<RegenerateInvoicePdf.Command, CancellationToken>((command, ct) => RegenerateHandler().Handle(command, ct));
        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(IMediator))).Returns(mediator.Object);
        services.Setup(s => s.GetService(typeof(ITenantProvider))).Returns(new Mock<ITenantProvider>().Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(services.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private async Task<BusinessResult<DownloadInvoice.Response>> Download()
    {
        var type = typeof(DownloadInvoice).GetNestedType("Handler", BindingFlags.Public | BindingFlags.NonPublic)!;
        var session = new TestUserSessionProvider("admin-user", "admin@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]);
        var handler = Activator.CreateInstance(type, _invoices.Object, new Mock<IOrderAccessService>().Object, session, _factory.Object)!;
        return await (Task<BusinessResult<DownloadInvoice.Response>>)type.GetMethod("Handle")!.Invoke(handler,
            [new DownloadInvoice.Query(_invoice.Id), CancellationToken.None])!;
    }

    private static Employee Cleaner()
    {
        var user = User.CreateWithPassword("jan.novak@cleansia.test", "Password1", "Jan", "Novák");
        var address = Address.Create("Dlouhá 12", "Praha", "11000", "cz");
        typeof(Address).GetProperty(nameof(Address.Country))!.SetValue(address, Country.Create("Czechia", "CZE", "CZ"));
        var employee = Employee.CreateWithUser(user);
        employee.UpdateAddress(address);
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
