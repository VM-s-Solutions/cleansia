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
/// (→ /decisions/adr-0083). A receipt whose file was never written, or whose stamped code is not in its
/// file, is owed a render until one is stored (→ /flows/payment-and-fiscal#a-receipt-s-pdf-is-repaired).
/// </summary>
public class ReceiptPdfVersionsTests
{
    private const string CountryId = "de";
    private const string FirstName = "2026/ORD-1/receipt_ORD-1_20261119.pdf";
    private const string SecondName = "2026/ORD-1/receipt_ORD-1_20261119-2.pdf";

    private readonly InMemoryBlobContainer _store = new(Constants.BlobContainers.GeneratedReceipts);
    private readonly Mock<IFiscalService> _fiscal = new();
    private int _uploadFaults;

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

        Assert.True(await service.CompleteReceiptAsync(receipt, order, pdfStored: true, CancellationToken.None));

        Assert.Equal("no code", Encoding.UTF8.GetString(_store.Blobs[FirstName]));
        Assert.Equal("SIG-1", Encoding.UTF8.GetString(_store.Blobs[SecondName]));
        Assert.Equal(SecondName, receipt.BlobName);
        Assert.Equal("SIG-1", Encoding.UTF8.GetString(await service.DownloadReceiptPdfAsync(receipt, CancellationToken.None)));
    }

    [Fact]
    public async Task An_Upload_Fault_After_The_Stamp_Leaves_The_Render_Owed_And_The_Next_Pass_Renders_The_Code_Without_Registering_Again()
    {
        var order = Order();
        var receipt = OrderReceipt.Create(order.Id, "RCP-1", "receipt_ORD-1_20261119.pdf", FirstName, "en");
        var service = Service();
        _fiscal.SetupSequence(f => f.RegisterReceiptAsync(It.IsAny<FiscalReceiptRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiscalResult.TransientError("TIMEOUT", "authority down"))
            .ReturnsAsync(FiscalResult.Success("SIG-1", "2026-11-19T10:00:00Z"));
        await service.RealizeFiscalAndPdfAsync(order, receipt, CancellationToken.None);

        _uploadFaults = 1;
        Assert.False(await service.CompleteReceiptAsync(receipt, order, pdfStored: true, CancellationToken.None));
        Assert.Equal("SIG-1", receipt.FiscalCode);
        Assert.NotNull(receipt.FiscalNextRetryAt);
        Assert.Equal(FirstName, receipt.BlobName);

        Assert.True(await service.CompleteReceiptAsync(receipt, order, pdfStored: true, CancellationToken.None));

        Assert.Equal("SIG-1", Encoding.UTF8.GetString(_store.Blobs[SecondName]));
        Assert.Equal(SecondName, receipt.BlobName);
        Assert.Null(receipt.FiscalNextRetryAt);
        Assert.Null(receipt.FiscalError);
        _fiscal.Verify(f => f.RegisterReceiptAsync(It.IsAny<FiscalReceiptRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task In_Mode_None_A_First_Upload_That_Failed_Is_Rendered_By_The_Next_Pass_Without_Registering()
    {
        var order = Order();
        var receipt = OrderReceipt.Create(order.Id, "RCP-1", "receipt_ORD-1_20261119.pdf", FirstName, "en");
        var service = Service(FiscalEnforcementMode.None);

        _uploadFaults = 1;
        await Assert.ThrowsAnyAsync<IOException>(() => service.RealizeFiscalAndPdfAsync(order, receipt, CancellationToken.None));
        Assert.False(await service.IsPdfStoredAsync(receipt, CancellationToken.None));

        Assert.True(await service.CompleteReceiptAsync(receipt, order, pdfStored: false, CancellationToken.None));

        Assert.Equal("no code", Encoding.UTF8.GetString(_store.Blobs[FirstName]));
        Assert.True(await service.IsPdfStoredAsync(receipt, CancellationToken.None));
        Assert.Null(receipt.FiscalNextRetryAt);
        _fiscal.Verify(f => f.RegisterReceiptAsync(It.IsAny<FiscalReceiptRequest>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Theory]
    [InlineData(FiscalEnforcementMode.None)]
    [InlineData(FiscalEnforcementMode.AsyncBackground)]
    public async Task A_Claim_Is_Born_Owed_Its_Render_In_Every_Mode_And_The_Realize_That_Stores_It_Settles_It(FiscalEnforcementMode mode)
    {
        var order = Order();
        var service = Service(mode);
        _fiscal.Setup(f => f.RegisterReceiptAsync(It.IsAny<FiscalReceiptRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiscalResult.Success("SIG-1", "2026-11-19T10:00:00Z"));

        var receipt = await service.ReserveReceiptAsync(order, "en", CancellationToken.None);

        Assert.True(receipt.FiscalNextRetryAt > DateTime.UtcNow);
        await service.RealizeFiscalAndPdfAsync(order, receipt, CancellationToken.None);
        Assert.Null(receipt.FiscalNextRetryAt);
    }

    private ReceiptService Service(FiscalEnforcementMode mode = FiscalEnforcementMode.AsyncBackground)
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
                .UpdateFiscalEnforcementMode(mode));
        _fiscal.SetupGet(f => f.ProviderKey).Returns("de-tse-test");
        var resolver = new Mock<IFiscalServiceResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(_fiscal.Object);
        var pdf = new Mock<IPdfService>();
        pdf.Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Returns<ReceiptPdfData, string?>((data, _) => Encoding.UTF8.GetBytes(data.FiscalCode ?? "no code"));
        var store = new Mock<IBlobContainerClient>();
        store.Setup(c => c.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(_store.ExistsAsync);
        store.Setup(c => c.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(_store.DownloadAsync);
        store.Setup(c => c.UploadIfAbsentAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<string, Stream, CancellationToken>((name, content, ct) =>
                _uploadFaults-- > 0 ? throw new IOException("503 ServerBusy") : _store.UploadIfAbsentAsync(name, content, ct));
        var blobs = new Mock<IBlobContainerClientFactory>();
        blobs.Setup(f => f.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts)).Returns(store.Object);
        var counter = new Mock<IFiscalCounterRepository>();
        counter.Setup(c => c.AllocateNextAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return new ReceiptService(
            pdf.Object,
            new Mock<IOrderReceiptRepository>().Object,
            counter.Object,
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
