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
/// The auto-close job invoices a cleaner ONCE PER CURRENCY their unassigned pay holds. A cleaner can
/// work a CZK order and a EUR order in one period, and a tax document is in one unit, so the period
/// yields one document per currency -- each with its own payout reference, each assigned exactly the
/// rows in its currency, and each sent in its own period-closed e-mail because the template carries
/// one attachment. A currency this pair already holds a document for is skipped, not re-invoiced.
///
/// The shape it replaces refused the whole period when the rows disagreed, and no admin action could
/// resolve that refusal: a EUR job cannot be made a CZK job.
/// </summary>
public class PayPeriodMixedCurrencyInvoicingTests
{
    private const string EurId = "currency-eur";
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

    private readonly List<EmployeeInvoice> _addedInvoices = [];
    private readonly List<(byte[]? PdfBytes, string? FileName)> _emailsSent = [];
    private List<EmployeeInvoice> _existingInvoices = [];
    private List<OrderEmployeePay> _unassignedPays = [];

    public PayPeriodMixedCurrencyInvoicingTests()
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

        // Evaluated lazily so a test can seed an already-invoiced currency before the run.
        _invoiceRepository
            .Setup(r => r.GetQueryable())
            .Returns(() => _existingInvoices.AsQueryable().BuildMock());
        _invoiceRepository
            .Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(invoice =>
            {
                _addedInvoices.Add(invoice);
                typeof(EmployeeInvoice)
                    .GetProperty(nameof(EmployeeInvoice.PayPeriod))!
                    .SetValue(invoice, _expiredPeriod);
            });

        _orderPayRepository
            .Setup(r => r.GetUnassignedForEmployeePeriodAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _unassignedPays);

        var czk = CurrencyMockFactory.Generate();
        czk.Id = PayrollMockFactory.CurrencyId;
        var eur = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "EUR", Symbol = "€" });
        eur.Id = EurId;
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(czk);
        _currencyRepository
            .Setup(r => r.GetByIdAsync(PayrollMockFactory.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(czk);
        _currencyRepository
            .Setup(r => r.GetByIdAsync(EurId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(eur);

        // A fresh symbol per allocation, as the real counter hands out.
        _payoutReferenceAllocator
            .Setup(a => a.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BusinessResult.Success(PayrollMockFactory.NextTestVariableSymbol()));

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

        _emailService
            .Setup(e => e.SendPeriodClosedEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<byte[]?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateOnly, DateOnly, DateTime, string, string, byte[]?, string?, CancellationToken>(
                (_, _, _, _, _, _, _, pdf, name, _) => _emailsSent.Add((pdf, name)))
            .ReturnsAsync("message-id");
    }

    [Fact]
    public async Task Pay_In_Two_Currencies_Yields_Two_Invoices_Each_Holding_Only_Its_Own_Rows()
    {
        var czkA = PayrollMockFactory.OrderPay(basePay: 600m);
        var czkB = PayrollMockFactory.OrderPay(basePay: 400m);
        var eur = PayrollMockFactory.OrderPay(basePay: 30m, currencyId: EurId);
        _unassignedPays = [czkA, eur, czkB];

        await Run();

        Assert.Equal(2, _addedInvoices.Count);
        var czkInvoice = Assert.Single(_addedInvoices, i => i.CurrencyId == PayrollMockFactory.CurrencyId);
        var eurInvoice = Assert.Single(_addedInvoices, i => i.CurrencyId == EurId);

        Assert.Equal(1000m, czkInvoice.TotalAmount);
        Assert.Equal(2, czkInvoice.TotalOrders);
        Assert.Equal(30m, eurInvoice.TotalAmount);
        Assert.Equal(1, eurInvoice.TotalOrders);

        // Two documents, two references.
        Assert.NotEqual(czkInvoice.VariableSymbol, eurInvoice.VariableSymbol);
        _payoutReferenceAllocator.Verify(a => a.AllocateAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        // Every row lands on the invoice in ITS currency.
        Assert.Equal(czkInvoice.Id, czkA.EmployeeInvoiceId);
        Assert.Equal(czkInvoice.Id, czkB.EmployeeInvoiceId);
        Assert.Equal(eurInvoice.Id, eur.EmployeeInvoiceId);
    }

    [Fact]
    public async Task Each_Invoice_Document_Goes_Out_In_Its_Own_Period_Closed_Email()
    {
        _unassignedPays =
        [
            PayrollMockFactory.OrderPay(basePay: 600m),
            PayrollMockFactory.OrderPay(basePay: 30m, currencyId: EurId),
        ];

        await Run();

        Assert.Equal(2, _emailsSent.Count);
        Assert.All(_emailsSent, sent =>
        {
            Assert.NotNull(sent.PdfBytes);
            Assert.NotNull(sent.FileName);
        });
        Assert.Equal(
            _addedInvoices.Select(i => $"{i.InvoiceNumber}.pdf").OrderBy(n => n, StringComparer.Ordinal),
            _emailsSent.Select(s => s.FileName!).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_Currency_This_Pair_Already_Holds_An_Invoice_For_Is_Skipped_Not_Doubled()
    {
        // The CZK half was invoiced when the period first closed; a EUR pay row has landed since.
        _existingInvoices = [PayrollMockFactory.Invoice(currencyId: PayrollMockFactory.CurrencyId)];
        var eur = PayrollMockFactory.OrderPay(basePay: 30m, currencyId: EurId);
        _unassignedPays = [PayrollMockFactory.OrderPay(basePay: 600m), eur];

        await Run();

        var written = Assert.Single(_addedInvoices);
        Assert.Equal(EurId, written.CurrencyId);
        Assert.Equal(written.Id, eur.EmployeeInvoiceId);
        _payoutReferenceAllocator.Verify(a => a.AllocateAsync(It.IsAny<CancellationToken>()), Times.Once);

        // One document, one e-mail -- not a second CZK document and not a bare e-mail.
        var sent = Assert.Single(_emailsSent);
        Assert.Equal($"{written.InvoiceNumber}.pdf", sent.FileName);
    }

    [Fact]
    public async Task Pay_In_One_Currency_Still_Yields_Exactly_One_Invoice_And_One_Email()
    {
        _unassignedPays = [PayrollMockFactory.OrderPay(basePay: 600m), PayrollMockFactory.OrderPay(basePay: 400m)];

        await Run();

        var written = Assert.Single(_addedInvoices);
        Assert.Equal(1000m, written.TotalAmount);
        var sent = Assert.Single(_emailsSent);
        Assert.Equal($"{written.InvoiceNumber}.pdf", sent.FileName);
    }

    [Fact]
    public async Task No_Unassigned_Pay_Sends_One_Plain_Period_Closed_Email_And_No_Invoice()
    {
        _unassignedPays = [];

        await Run();

        Assert.Empty(_addedInvoices);
        var sent = Assert.Single(_emailsSent);
        Assert.Null(sent.PdfBytes);
        Assert.Null(sent.FileName);
        _payoutReferenceAllocator.Verify(a => a.AllocateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A currency whose reference cannot be claimed loses ITS document only; the other currency's
    /// document is still written and sent. The failure is per document, not per cleaner.
    /// </summary>
    [Fact]
    public async Task A_Reference_Refused_For_One_Currency_Does_Not_Cost_The_Other_Its_Invoice()
    {
        var calls = 0;
        _payoutReferenceAllocator
            .Setup(a => a.AllocateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++calls == 1
                ? BusinessResult.Failure<string>(new Error("VariableSymbol", "payroll.invoice.reference_unavailable"))
                : BusinessResult.Success(PayrollMockFactory.NextTestVariableSymbol()));
        _unassignedPays =
        [
            PayrollMockFactory.OrderPay(basePay: 600m),
            PayrollMockFactory.OrderPay(basePay: 30m, currencyId: EurId),
        ];

        await Run();

        // Groups run in ordinal key order: "currency-1" (CZK) is refused, "currency-eur" succeeds.
        var written = Assert.Single(_addedInvoices);
        Assert.Equal(EurId, written.CurrencyId);
        var sent = Assert.Single(_emailsSent);
        Assert.Equal($"{written.InvoiceNumber}.pdf", sent.FileName);
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
