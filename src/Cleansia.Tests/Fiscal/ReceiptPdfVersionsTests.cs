using System.Text;
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
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Constants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Fiscal;

/// <summary>
/// The fiscal retry renders an issued receipt again with its fiscal code. The new render is a new file
/// next to the first, the receipt points at it, and the first stays as the customer got it
/// (→ /decisions/adr-0083).
/// </summary>
public class ReceiptPdfVersionsTests
{
    private const string CountryId = "de";
    private const string FirstName = "2026/ORD-1/receipt_ORD-1_20261119.pdf";
    private const string SecondName = "2026/ORD-1/receipt_ORD-1_20261119-2.pdf";

    private readonly InMemoryBlobContainer _store = new(Constants.BlobContainers.GeneratedReceipts);
    private readonly Mock<IFiscalService> _fiscal = new();

    [Fact]
    public async Task A_Fiscal_Retry_Writes_A_New_Receipt_File_With_The_Code_And_Keeps_The_First()
    {
        var order = Order();
        var receipt = OrderReceipt.Create(order.Id, "RCP-1", "receipt_ORD-1_20261119.pdf", FirstName, "en");
        var service = Service();
        _fiscal.SetupSequence(f => f.RegisterReceiptAsync(It.IsAny<FiscalReceiptRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiscalResult.TransientError("TIMEOUT", "authority down"))
            .ReturnsAsync(FiscalResult.Success("SIG-1", "2026-11-19T10:00:00Z"));

        await service.RealizeFiscalAndPdfAsync(order, receipt, CancellationToken.None);
        Assert.Equal("no code", Encoding.UTF8.GetString(_store.Blobs[FirstName]));

        Assert.True(await service.RetryFiscalRegistrationAsync(receipt, order, CancellationToken.None));

        Assert.Equal("no code", Encoding.UTF8.GetString(_store.Blobs[FirstName]));
        Assert.Equal("SIG-1", Encoding.UTF8.GetString(_store.Blobs[SecondName]));
        Assert.Equal(SecondName, receipt.BlobName);
        Assert.Equal("SIG-1", Encoding.UTF8.GetString(await service.DownloadReceiptPdfAsync(receipt, CancellationToken.None)));
    }

    private ReceiptService Service()
    {
        var language = new Mock<ILanguageRepository>();
        language.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Language.Create("en", "English"));
        var companies = new Mock<ICompanyInfoRepository>();
        companies.Setup(r => r.GetActiveByCountryAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyInfo.Create("Cleansia GmbH", "Cleansia", "HRB-1", "Hauptstr. 1", "Berlin", "10115", CountryId));
        var countries = new Mock<ICountryRepository>();
        countries.Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Germany", "DEU", "DE"));
        var configurations = new Mock<ICountryConfigurationRepository>();
        configurations.Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CountryId, "EUR", "en", standardVatRate: 19m)
                .UpdateFiscalEnforcementMode(FiscalEnforcementMode.AsyncBackground));
        _fiscal.SetupGet(f => f.ProviderKey).Returns("de-tse-test");
        var resolver = new Mock<IFiscalServiceResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(_fiscal.Object);
        var pdf = new Mock<IPdfService>();
        pdf.Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Returns<ReceiptPdfData, string?>((data, _) => Encoding.UTF8.GetBytes(data.FiscalCode ?? "no code"));
        var blobs = new Mock<IBlobContainerClientFactory>();
        blobs.Setup(f => f.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts)).Returns(_store);

        return new ReceiptService(
            pdf.Object,
            new Mock<IOrderReceiptRepository>().Object,
            new Mock<IFiscalCounterRepository>().Object,
            language.Object,
            companies.Object,
            countries.Object,
            configurations.Object,
            blobs.Object,
            resolver.Object,
            NullLogger<ReceiptService>.Instance);
    }

    private static Order Order()
    {
        var order = Core.Domain.Orders.Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+490000000000",
            customerAddress: Address.Create("Hauptstr. 2", "Berlin", "10115", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "eur",
            paymentStatus: PaymentStatus.Pending,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        var euro = Currency.Create("EUR", "€", "Euro");
        euro.Id = "eur";
        order.SetCurrency(euro);
        order.Id = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";
        return order;
    }
}
