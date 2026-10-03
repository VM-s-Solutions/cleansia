using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Fiscal;

/// <summary>
/// An order can hold more than one receipt: its sale receipt and a fee receipt for each receivable paid on
/// it. The fee receipt takes its own number from the same counter and names its receivable; the order's
/// receipt stays the sale's. It states the fee alone — one line, the fee's total, paid by card — on the
/// document in the customer's language and on the request to the fiscal authority.
/// </summary>
public class FeeReceiptTests
{
    private const string CountryId = "de";
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4F7";

    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<IOrderReceiptRepository> _receiptRepository = new();
    private readonly Mock<IFiscalCounterRepository> _fiscalCounterRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly Mock<IFiscalServiceResolver> _fiscalServiceResolver = new();
    private readonly RecordingFiscalProvider _provider = new();
    private readonly Language _czech = Language.Create("cs", "Čeština");
    private ReceiptPdfData? _document;

    public FeeReceiptTests()
    {
        _languageRepository.Setup(r => r.GetByCodeAsync("cs", It.IsAny<CancellationToken>())).ReturnsAsync(_czech);
        _languageRepository.Setup(r => r.GetByIdAsync(_czech.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_czech);

        var company = CompanyInfo.Create(
            legalName: "Cleansia GmbH",
            tradingName: "Cleansia",
            registrationNumber: "HRB-1",
            street: "Hauptstr. 1",
            city: "Berlin",
            zipCode: "10115",
            countryId: CountryId);
        _companyInfoRepository.Setup(r => r.GetActiveByCountryAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _countryRepository
            .Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Germany", "DEU", "DE"));
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration
                .Create(CountryId, "EUR", "cs", standardVatRate: 0.19m)
                .UpdateFiscalEnforcementMode(FiscalEnforcementMode.BlockingOnline));
        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(_provider);
        _fiscalCounterRepository
            .Setup(r => r.AllocateNextAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7L);
        _pdfService
            .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Callback((ReceiptPdfData data, string? _) => _document = data)
            .Returns([1, 2, 3]);
        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(new Mock<IBlobContainerClient>().Object);
    }

    private ReceiptService CreateService() => new(
        _pdfService.Object,
        _receiptRepository.Object,
        _fiscalCounterRepository.Object,
        _languageRepository.Object,
        _companyInfoRepository.Object,
        _countryRepository.Object,
        _countryConfigurationRepository.Object,
        _blobClientFactory.Object,
        _fiscalServiceResolver.Object,
        NullLogger<ReceiptService>.Instance);

    private static Order PaidOrder()
    {
        var order = Order.Create(
            customerName: "Owing Customer",
            customerEmail: "owing@example.com",
            customerPhone: "+490000000000",
            customerAddress: Address.Create("Hauptstr. 2", "Berlin", "10115", CountryId),
            rooms: 3,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 2000m,
            currencyId: "eur",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-owing",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        var euro = Currency.Create("EUR", "€", "Euro");
        euro.Id = "eur";
        order.SetCurrency(euro);
        return order;
    }

    private static Receivable PaidFee(Order order)
    {
        var receivable = Receivable.ForCashCancellationFee(order, 500m);
        receivable.MarkPaid("pi_link", DateTimeOffset.UtcNow);
        return receivable;
    }

    [Fact]
    public async Task A_Fee_Receipt_Takes_Its_Own_Number_And_Names_Its_Receivable_While_The_Order_Keeps_Its_Sale_Receipt()
    {
        var order = PaidOrder();
        var sale = OrderReceipt.Create(OrderId, "RCP-2026-0006", "receipt.pdf", "2026/ORD/receipt.pdf", _czech.Id);
        OrderReceiptAttachment.Attach(order, sale);
        var receivable = PaidFee(order);

        var fee = await CreateService().ReserveFeeReceiptAsync(order, receivable, "cs", CancellationToken.None);
        OrderReceiptAttachment.Attach(order, fee);

        Assert.Equal($"RCP-{DateTime.UtcNow.Year}-0007", fee.ReceiptNumber);
        Assert.Equal((OrderId, receivable.Id), (fee.OrderId, fee.ReceivableId));
        Assert.NotEqual(sale.BlobName, fee.BlobName);
        _receiptRepository.Verify(r => r.Add(fee), Times.Once);
        Assert.Same(sale, order.Receipt);
        Assert.Equal(2, order.Receipts.Count);
    }

    [Fact]
    public async Task A_Fee_Receipt_States_The_Fee_Alone_On_The_Document_And_To_The_Fiscal_Authority()
    {
        var order = PaidOrder();
        var receivable = PaidFee(order);
        var fee = await CreateService().ReserveFeeReceiptAsync(order, receivable, "cs", CancellationToken.None);

        await CreateService().RealizeFiscalAndPdfAsync(order, fee, CancellationToken.None);

        var line = Assert.Single(_document!.Services);
        Assert.Equal(("Poplatek za pozdní zrušení", 500m), (line.Name, line.Price));
        Assert.Empty(_document.Packages);
        Assert.Equal((500m, PaymentStatus.Paid, PaymentType.Card), (_document.Total, _document.PaymentStatus, _document.PaymentType));

        var request = _provider.LastRequest!;
        Assert.Equal(500m, request.TotalAmount);
        var fiscalLine = Assert.Single(request.LineItems);
        Assert.Equal(("Late cancellation fee", 500m), (fiscalLine.Description, fiscalLine.UnitPrice));
        Assert.Equal(fee.ReceiptNumber, request.ReceiptNumber);
        Assert.NotNull(fee.FiscalCode);
    }

    private sealed class RecordingFiscalProvider : IFiscalService
    {
        public string ProviderKey => "de-tse-test";
        public string CountryCode => "DE";
        public bool RegisterIsIdempotent => true;
        public FiscalReceiptRequest? LastRequest { get; private set; }

        public Task<FiscalResult> RegisterReceiptAsync(FiscalReceiptRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(FiscalResult.Success($"SIG-{request.IdempotencyKey}", request.IssuedAt.ToString("o")));
        }
    }
}
