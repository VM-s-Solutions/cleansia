using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28: a cash receipt is issued once, at completion, after the cleaner
/// recorded the cash — so it is the one document that can state the booked slot, when the clean was
/// completed and when the cash changed hands, all in the market's own time. A card receipt, issued when
/// the payment settles, states neither of the last two.
/// </summary>
public class CashReceiptAtCompletionTests
{
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";
    private const string CountryId = "cz";
    private const string LanguageId = "lang-cs";

    [Fact]
    public async Task The_Receipt_States_The_Slot_The_Completion_And_The_Cash_In_Market_Time()
    {
        var fixture = new RenderFixture();
        var order = CompletedCashSale(
            cleaningUtc: new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc),
            collectedUtc: new DateTime(2026, 7, 15, 10, 5, 0, DateTimeKind.Utc),
            completedUtc: new DateTime(2026, 7, 15, 10, 20, 0, DateTimeKind.Utc));

        await fixture.Service().RealizeFiscalAndPdfAsync(order, Receipt(), CancellationToken.None);

        var rendered = fixture.LastRendered!;
        Assert.Equal("15.07.2026 10:00", rendered.CleaningDate);
        Assert.Equal("15.07.2026 12:20", rendered.CompletedAt);
        Assert.Equal("15.07.2026 12:05", rendered.CashReceivedAt);
        Assert.Equal(PaymentStatus.Paid, rendered.PaymentStatus);
    }

    [Fact]
    public async Task A_Card_Receipt_Issued_At_Payment_States_Neither_Date()
    {
        var fixture = new RenderFixture();
        var order = BuildOrder(PaymentType.Card, new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc));
        order.UpdatePaymentStatus(PaymentStatus.Paid);

        await fixture.Service().RealizeFiscalAndPdfAsync(order, Receipt(), CancellationToken.None);

        Assert.Null(fixture.LastRendered!.CompletedAt);
        Assert.Null(fixture.LastRendered.CashReceivedAt);
    }

    /// <summary>The date is the market's calendar day, not UTC's: 22:30 UTC is already the next day in Prague.</summary>
    [Fact]
    public async Task A_Receipt_Issued_Just_After_Local_Midnight_Carries_The_Local_Day()
    {
        var fixture = new RenderFixture();
        var receipt = Receipt();
        typeof(OrderReceipt).GetProperty(nameof(OrderReceipt.IssuedAt))!
            .SetValue(receipt, new DateTime(2026, 9, 1, 22, 30, 0, DateTimeKind.Utc));

        await fixture.Service().RealizeFiscalAndPdfAsync(
            CompletedCashSale(DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow), receipt, CancellationToken.None);

        Assert.Equal("02.09.2026", fixture.LastRendered!.IssuedDate);
    }

    /// <summary>The document is written in the language recorded on the receipt row.</summary>
    [Fact]
    public async Task The_Issued_Document_Is_Written_In_The_Receipts_Language()
    {
        var fixture = new RenderFixture();

        await fixture.Service().RealizeFiscalAndPdfAsync(
            CompletedCashSale(DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow), Receipt(), CancellationToken.None);

        Assert.Equal("cs", fixture.LastRendered!.LanguageCode);
    }

    [Fact]
    public void The_Layout_Prints_The_Completion_Beside_The_Slot_And_The_Cash_Beside_The_Payment()
    {
        var layout = new ProbeLayout();
        var data = new ReceiptPdfData
        {
            ReceiptNumber = "2026-000001",
            OrderNumber = "ORD-1",
            IssuedDate = "15.07.2026",
            CustomerName = "Jan Novák",
            LanguageCode = "cs",
            Services = [],
            Packages = [],
            Total = 1000m,
            Currency = "Kč",
            PaymentStatus = PaymentStatus.Paid,
            PaymentType = PaymentType.Cash,
            CleaningDate = "15.07.2026 10:00",
            CompletedAt = "15.07.2026 12:20",
            CashReceivedAt = "15.07.2026 12:05",
        };

        Assert.Contains(("Termín úklidu", "15.07.2026 10:00"), layout.Details(data));
        Assert.Contains(("Úklid dokončen", "15.07.2026 12:20"), layout.Details(data));
        Assert.Contains(("Hotovost přijata", "15.07.2026 12:05"), layout.Payment(data));

        var card = data with { PaymentType = PaymentType.Card, CompletedAt = null, CashReceivedAt = null };
        Assert.DoesNotContain(layout.Details(card), line => line.Label == "Úklid dokončen");
        Assert.DoesNotContain(layout.Payment(card), line => line.Label == "Hotovost přijata");
    }

    private static Order CompletedCashSale(DateTime cleaningUtc, DateTime collectedUtc, DateTime completedUtc)
    {
        var order = BuildOrder(PaymentType.Cash, cleaningUtc);
        order.MarkCashCollected("employee-1", collectedUtc);
        order.MarkCompletedAt(completedUtc);
        return order;
    }

    private static Order BuildOrder(PaymentType paymentType, DateTime cleaningUtc)
    {
        var order = Order.Create(
            customerName: "Jan Novák",
            customerEmail: "jan@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Hlavní 2", "Praha", "11000", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: cleaningUtc,
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        var czk = Currency.Create("CZK", "Kč", "Czech Koruna");
        czk.Id = "czk";
        order.SetCurrency(czk);
        return order;
    }

    private static OrderReceipt Receipt() =>
        OrderReceipt.Create(OrderId, "2026-000001", "receipt.pdf", "2026/ORD/receipt.pdf", LanguageId);

    private sealed class ProbeLayout : DefaultReceiptLayoutBuilder
    {
        public IReadOnlyList<(string Label, string Value)> Details(ReceiptPdfData data) => OrderDetailLines(data);

        public IReadOnlyList<(string Label, string Value)> Payment(ReceiptPdfData data) => PaymentLines(data);
    }

    /// <summary>A real <see cref="ReceiptService"/> over doubles, in a Prague market, recording what it rendered.</summary>
    private sealed class RenderFixture
    {
        private readonly Mock<IPdfService> _pdfService = new();
        private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
        private readonly Mock<ILanguageRepository> _languageRepository = new();
        private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
        private readonly Mock<ICountryRepository> _countryRepository = new();
        private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();

        public ReceiptPdfData? LastRendered { get; private set; }

        public RenderFixture()
        {
            _pdfService
                .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
                .Callback<ReceiptPdfData, string?>((data, _) => LastRendered = data)
                .Returns([37, 80, 68, 70]);
            _blobClientFactory
                .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
                .Returns(Mock.Of<IBlobContainerClient>());
            _languageRepository
                .Setup(r => r.GetByIdAsync(LanguageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Language.Create("cs", "Čeština"));

            var company = CompanyInfo.Create(
                legalName: "Cleansia s.r.o.",
                tradingName: "Cleansia",
                registrationNumber: "12345678",
                street: "Hlavní 1",
                city: "Praha",
                zipCode: "11000",
                countryId: CountryId);
            _companyInfoRepository
                .Setup(r => r.GetActiveByCountryAsync(CountryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(company);
            _countryRepository
                .Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Country.Create("Czechia", "CZE", "CZ"));
            _countryConfigurationRepository
                .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CountryConfiguration.Create(CountryId, "czk", "cs", standardVatRate: 21m, timeZoneId: "Europe/Prague"));
        }

        public ReceiptService Service() => new(
            _pdfService.Object,
            Mock.Of<IOrderReceiptRepository>(),
            Mock.Of<IFiscalCounterRepository>(),
            _languageRepository.Object,
            _companyInfoRepository.Object,
            _countryRepository.Object,
            _countryConfigurationRepository.Object,
            _blobClientFactory.Object,
            Mock.Of<IFiscalServiceResolver>(),
            NullLogger<ReceiptService>.Instance);
    }
}
