using System.Reflection;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
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
using Cleansia.Infra.Database;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using MediatR;
using Moq;
using MockQueryable;
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
    private EmployeeInvoice _invoice = PayrollMockFactory.Invoice(payPeriod: PayrollMockFactory.OpenPeriod());
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
            .Setup(c => c.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blobContainerClient
            .Setup(c => c.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => _downloadedNames.Add(name))
            .ReturnsAsync(() => new BlobFile(new MemoryStream([1, 2, 3]), "application/pdf"));
        _invoiceRepository
            .Setup(r => r.GetByIdAsync(_invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_invoice);
        _invoiceRepository.Setup(r => r.GetAll()).Returns(() => new[] { _invoice }.AsQueryable().BuildMock());
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_New_Invoice_Renders_After_Commit_And_Preserves_Financial_Success_When_Rendering_Fails(bool emptyRender)
    {
        var commits = new List<(string? Pdf, bool Failed)>();
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>())).Callback<EmployeeInvoice>(invoice => _invoice = invoice);
        _invoiceRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _invoice);
        _invoiceRepository.Setup(r => r.GetAll()).Returns(() =>
        {
            typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.PayPeriod))!
                .SetValue(_invoice, PayrollMockFactory.OpenPeriod());
            return new[] { _invoice }.AsQueryable().BuildMock();
        });
        _invoiceRepository.Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => commits.Add((_invoice.PdfBlobUrl, _invoice.PdfGenerationFailed)))
            .Returns(Task.CompletedTask);
        var pays = new Mock<IOrderEmployeePayRepository>();
        var pay = PayrollMockFactory.OrderPay(basePay: 100m);
        pays.Setup(r => r.GetUnassignedForEmployeePeriodAsync(pay.EmployeeId, pay.PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pay]);
        var allocator = new Mock<IPayoutReferenceAllocator>();
        allocator.Setup(a => a.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(PayrollMockFactory.TestVariableSymbol));
        allocator.Setup(a => a.AllocateInvoiceNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(PayrollMockFactory.TestInvoiceNumber));
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => _invoiceRepository.Object.CommitAsync(ct));
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<RegenerateInvoicePdf.Command>(), It.IsAny<CancellationToken>()))
            .Returns<RegenerateInvoicePdf.Command, CancellationToken>((command, ct) =>
            {
                Assert.Single(commits);
                Assert.Null(commits[0].Pdf);
                Assert.Equal(_invoice.Id, command.InvoiceId);
                Assert.Equal(_invoice.Id, pay.EmployeeInvoiceId);
                var pipeline = new UnitOfWorkPipelineBehavior<RegenerateInvoicePdf.Command, BusinessResult<RegenerateInvoicePdf.Response>>(unitOfWork.Object);
                return pipeline.Handle(command, _ => RegenerateAsync(emptyRender), ct);
            });
        var services = new ServiceCollection();
        services.AddSingleton(mediator.Object);
        services.AddScoped<ITenantProvider>(_ => new Mock<ITenantProvider>().Object);
        await using var root = services.BuildServiceProvider();
        var handler = new GenerateInvoice.Handler(_invoiceRepository.Object, pays.Object, allocator.Object,
            new Mock<ICashLedgerRepository>().Object, new Mock<IPayPeriodRepository>().Object, new Mock<IEmployeeRepository>().Object,
            new Mock<IEmailService>().Object, _blobContainerClientFactory.Object, root.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<GenerateInvoice.Handler>.Instance);

        var created = await handler.Handle(new GenerateInvoice.Command(pay.EmployeeId, pay.PayPeriodId), CancellationToken.None);
        var downloaded = await DownloadAsync();

        Assert.True(created.IsSuccess);
        Assert.Equal(_invoice.Id, Assert.Single(created.Value.InvoiceIds));
        Assert.Equal(100m, _invoice.TotalAmount);
        Assert.Equal(2, commits.Count);
        Assert.Equal(emptyRender, commits[1].Failed);
        if (emptyRender)
        {
            Assert.Null(commits[1].Pdf);
            Assert.True(downloaded.IsFailure);
            Assert.Equal(Cleansia.Core.AppServices.Common.BusinessErrorMessage.PdfGenerationFailed, downloaded.Error!.Message);
        }
        else
        {
            Assert.NotNull(commits[1].Pdf);
            Assert.True(downloaded.IsSuccess);
            Assert.Equal(Assert.Single(_uploadedNames), Assert.Single(_downloadedNames));
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Each_Currency_Renders_In_A_Fresh_Tenant_Scope_After_A_Failed_Commit(bool firstRenderEmpty, bool adminRequest)
    {
        const string tenant = "tenant-approved";
        var invoices = new List<EmployeeInvoice>();
        var financiallyCommitted = false;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>())).Callback<EmployeeInvoice>(invoices.Add);
        _invoiceRepository.Setup(r => r.CommitAsync(It.IsAny<CancellationToken>())).Callback(() =>
        {
            foreach (var invoice in invoices) invoice.TenantId = tenant;
            financiallyCommitted = true;
        }).Returns(Task.CompletedTask);
        var pays = new Mock<IOrderEmployeePayRepository>();
        var first = PayrollMockFactory.OrderPay(currencyId: "currency-1");
        var second = PayrollMockFactory.OrderPay(currencyId: "currency-2");
        pays.Setup(r => r.GetUnassignedForEmployeePeriodAsync(first.EmployeeId, first.PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        var allocator = new Mock<IPayoutReferenceAllocator>();
        allocator.Setup(a => a.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BusinessResult.Success(PayrollMockFactory.NextTestVariableSymbol()));
        allocator.Setup(a => a.AllocateInvoiceNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BusinessResult.Success(PayrollMockFactory.NextTestInvoiceNumber()));
        var scopeOrdinals = new Dictionary<IEmployeeInvoiceRepository, int>();
        var rollbacks = new List<int>();
        var rendered = new List<(int Scope, bool Success)>();
        var units = new List<Mock<IUnitOfWork>>();
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
        {
            HttpContext = adminRequest ? new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "admin-actor"),
                    new Claim(TenantProvider.TenantClaimType, "wrong-default-tenant")
                ]))
            } : null
        });
        services.AddScoped<IUserSessionProvider, UserSessionProvider>();
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<IEmployeeInvoiceRepository>(provider =>
        {
            var repository = new Mock<IEmployeeInvoiceRepository>();
            var ordinal = scopeOrdinals.Count + 1;
            scopeOrdinals.Add(repository.Object, ordinal);
            repository.Setup(r => r.GetAll()).Returns(() =>
            {
                Assert.Equal(tenant, provider.GetRequiredService<ITenantProvider>().GetCurrentTenantId());
                foreach (var invoice in invoices)
                    typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.PayPeriod))!
                        .SetValue(invoice, PayrollMockFactory.OpenPeriod());
                return invoices.AsQueryable().BuildMock();
            });
            if (ordinal == 1)
                repository.Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new IOException("failure-state persistence unavailable"));
            else
                repository.Setup(r => r.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            return repository.Object;
        });
        services.AddScoped<IUnitOfWork>(provider =>
        {
            var repository = provider.GetRequiredService<IEmployeeInvoiceRepository>();
            var unit = new Mock<IUnitOfWork>();
            unit.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(repository.CommitAsync);
            unit.Setup(u => u.Rollback()).Callback(() => rollbacks.Add(scopeOrdinals[repository]));
            units.Add(unit);
            return unit.Object;
        });
        services.AddScoped<IMediator>(provider =>
        {
            var repository = provider.GetRequiredService<IEmployeeInvoiceRepository>();
            var ordinal = scopeOrdinals[repository];
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<RegenerateInvoicePdf.Command>(), It.IsAny<CancellationToken>()))
                .Returns<RegenerateInvoicePdf.Command, CancellationToken>(async (command, ct) =>
                {
                    Assert.True(financiallyCommitted);
                    Assert.Equal(adminRequest ? "admin-actor" : null, provider.GetRequiredService<IUserSessionProvider>().GetUserId());
                    _invoice = invoices.Single(i => i.Id == command.InvoiceId);
                    var pipeline = new UnitOfWorkPipelineBehavior<RegenerateInvoicePdf.Command, BusinessResult<RegenerateInvoicePdf.Response>>(
                        provider.GetRequiredService<IUnitOfWork>());
                    try
                    {
                        var result = await pipeline.Handle(command, _ => RegenerateAsync(ordinal == 1 && firstRenderEmpty, repository), ct);
                        rendered.Add((ordinal, result.IsSuccess));
                        return result;
                    }
                    catch (IOException)
                    {
                        rendered.Add((ordinal, false));
                        throw;
                    }
                });
            return mediator.Object;
        });
        await using var root = services.BuildServiceProvider();
        var handler = new GenerateInvoice.Handler(_invoiceRepository.Object, pays.Object, allocator.Object,
            new Mock<ICashLedgerRepository>().Object, new Mock<IPayPeriodRepository>().Object, new Mock<IEmployeeRepository>().Object,
            new Mock<IEmailService>().Object, _blobContainerClientFactory.Object, root.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<GenerateInvoice.Handler>.Instance);

        var result = await handler.Handle(new GenerateInvoice.Command(first.EmployeeId, first.PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.InvoiceIds.Count);
        Assert.Equal(new[] { (1, false), (2, true) }, rendered);
        Assert.Equal(new[] { 1 }, rollbacks);
        Assert.Equal(firstRenderEmpty, invoices[0].PdfBlobUrl is null);
        Assert.NotNull(invoices[1].PdfBlobUrl);
        Assert.All(units, unit => unit.Verify(u => u.Dispose(), Times.Once));
    }

    private Task<BusinessResult<RegenerateInvoicePdf.Response>> RegenerateAsync(bool emptyRender = false, IEmployeeInvoiceRepository? repository = null)
    {
        var pdfService = new Mock<IPdfService>();
        pdfService
            .Setup(s => s.GenerateInvoicePdf(It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Returns(emptyRender ? [] : [1, 2, 3]);
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
            repository ?? _invoiceRepository.Object,
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
