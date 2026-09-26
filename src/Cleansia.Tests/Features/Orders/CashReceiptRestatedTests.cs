using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A CASH ORDER THAT HAS BEEN PAID STOPS SAYING IT IS UNPAID.
///
/// <para>A cash sale's receipt is issued when the booking is made — there is no payment event to hang
/// it on — so it is written while the order is still <see cref="PaymentStatus.Pending"/>. The cleaner
/// records the cash at the door hours later, and nothing came back for the document: the only paper a
/// cash customer ever held said their clean was unpaid, permanently.</para>
///
/// <para><b>The number does not change.</b> A numbered document re-issued under a new number is a
/// second document and a decision nobody has taken; this restates the one that exists, over the same
/// number, the same blob, the same issue date and the same language. It registers nothing with any
/// fiscal authority — that happened once, at issue, under this number — and it sends no e-mail.</para>
/// </summary>
public class CashReceiptRestatedTests
{
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";
    private const string EmployeeId = "emp-1";
    private const string CountryId = "cz";
    private const string LanguageId = "lang-cs";
    private const string ReceiptNumber = "2026-000001";
    private const string BlobName = "2026/ORD/receipt.pdf";

    // ── the producer ────────────────────────────────────────────────────────

    [Fact]
    public async Task Recording_The_Cash_Asks_For_The_Receipt_To_Be_Restated()
    {
        var pending = new RecordingPendingDispatch();
        var order = ArrangeOrder(withReceipt: true);

        var result = await CreateHandler(pending).Handle(
            new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var enqueued = Assert.Single(pending.Messages);
        Assert.Equal(QueueNames.GenerateReceipt, enqueued.QueueName);
        Assert.Equal(MessageKeys.ReceiptReissue(OrderId), enqueued.MessageKey);
        Assert.True(enqueued.Message.Reissue);
        Assert.Equal(OrderId, enqueued.Message.OrderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    /// <summary>
    /// No document, nothing to restate. The order's receipt is issued by the queue on its own terms,
    /// and minting one from here would be a different change.
    /// </summary>
    [Fact]
    public async Task An_Order_With_No_Receipt_Yet_Asks_For_Nothing()
    {
        var pending = new RecordingPendingDispatch();
        ArrangeOrder(withReceipt: false);

        var result = await CreateHandler(pending).Handle(
            new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(pending.Messages);
    }

    /// <summary>
    /// A refused collection took no money, so the document is still right. Nothing is even recorded —
    /// the post-commit behaviour drains only on success, but a buffered message on a failed command is
    /// a trap waiting for that predicate to change.
    /// </summary>
    [Fact]
    public async Task A_Refused_Collection_Asks_For_Nothing()
    {
        var pending = new RecordingPendingDispatch();
        var order = ArrangeOrder(withReceipt: true);
        order.ApplyCredit(100m, "customer");

        var result = await CreateHandler(pending).Handle(
            new MarkCashCollected.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(pending.Messages);
    }

    // ── the consumer ────────────────────────────────────────────────────────

    [Fact]
    public async Task The_Restate_Message_Re_Renders_And_Does_Nothing_Else()
    {
        var receiptService = new Mock<IReceiptService>();
        var emailService = new Mock<IEmailService>();
        var order = BuildOrder(PaymentStatus.Paid);
        var receipt = BuildReceipt();
        AttachReceipt(order, receipt);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await CreateQueueHandler(orderRepository, receiptService.Object, emailService)
            .HandleAsync(ReissueBody(), CancellationToken.None);

        receiptService.Verify(
            s => s.RegenerateReceiptPdfAsync(order, receipt, It.IsAny<CancellationToken>()), Times.Once);
        receiptService.Verify(
            s => s.ReserveReceiptAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        receiptService.Verify(
            s => s.RealizeFiscalAndPdfAsync(It.IsAny<Order>(), It.IsAny<OrderReceipt>(), It.IsAny<CancellationToken>()),
            Times.Never);
        emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Never);
    }

    /// <summary>
    /// A restate that races ahead of the issue it belongs to does NOT fall through into issuing one —
    /// that path allocates a fiscal number and e-mails the customer, and this message asked for
    /// neither.
    /// </summary>
    [Fact]
    public async Task A_Restate_With_No_Receipt_To_Restate_Issues_Nothing()
    {
        var receiptService = new Mock<IReceiptService>();
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrder(PaymentStatus.Paid));

        await CreateQueueHandler(orderRepository, receiptService.Object, new Mock<IEmailService>())
            .HandleAsync(ReissueBody(), CancellationToken.None);

        receiptService.Verify(
            s => s.ReserveReceiptAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        receiptService.Verify(
            s => s.RegenerateReceiptPdfAsync(It.IsAny<Order>(), It.IsAny<OrderReceipt>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// SAFE UNDER QUEUE RETRIES. The restate message delivered twice restates the one document twice —
    /// against a store that, like Azure's, refuses to create a blob that already exists — and never
    /// allocates a number or e-mails anyone.
    /// </summary>
    [Fact]
    public async Task A_Redelivered_Restate_Restates_The_Same_Document_Again_And_Nothing_Else()
    {
        var fixture = new RenderFixture();
        var order = BuildOrder(PaymentStatus.Pending);
        var receipt = BuildReceipt();
        await fixture.Service().RealizeFiscalAndPdfAsync(order, receipt, CancellationToken.None);
        AttachReceipt(order, receipt);
        order.MarkCashCollected(EmployeeId);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var emailService = new Mock<IEmailService>();
        var handler = CreateQueueHandler(orderRepository, fixture.Service(), emailService);

        await handler.HandleAsync(ReissueBody(), CancellationToken.None);
        await handler.HandleAsync(ReissueBody(), CancellationToken.None);

        Assert.Equal([BlobName], fixture.StoredBlobNames);
        Assert.Equal(nameof(PaymentStatus.Paid), fixture.StoredDocument(BlobName));
        Assert.Equal(ReceiptNumber, fixture.LastRendered!.ReceiptNumber);
        fixture.FiscalCounters.Verify(
            c => c.AllocateNextAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Never);
    }

    // ── the document itself ─────────────────────────────────────────────────

    /// <summary>
    /// The end the customer sees: the same number, the same blob — now holding the restated document —
    /// and the payment status the order actually reached.
    /// </summary>
    [Fact]
    public async Task The_Restated_Document_Says_Paid_Under_The_Same_Number()
    {
        var fixture = new RenderFixture();
        var order = BuildOrder(PaymentStatus.Pending);
        var receipt = BuildReceipt();

        await fixture.Service().RealizeFiscalAndPdfAsync(order, receipt, CancellationToken.None);
        var issued = fixture.LastRendered!;

        order.MarkCashCollected(EmployeeId);
        await fixture.Service().RegenerateReceiptPdfAsync(order, receipt, CancellationToken.None);
        var restated = fixture.LastRendered!;

        Assert.Equal(PaymentStatus.Pending, issued.PaymentStatus);
        Assert.Equal(PaymentStatus.Paid, restated.PaymentStatus);
        Assert.Equal(issued.ReceiptNumber, restated.ReceiptNumber);
        Assert.Equal([BlobName], fixture.StoredBlobNames);
        Assert.Equal(nameof(PaymentStatus.Paid), fixture.StoredDocument(BlobName));
    }

    /// <summary>
    /// THE ISSUED DOCUMENT SURVIVES A RESTATE THAT FAILS. Opening the blob writer re-creates the blob
    /// empty, so a writer opened before the render meant a render that threw — or a queue retry that
    /// threw five times — left the customer's only receipt at zero bytes under its number.
    /// </summary>
    [Fact]
    public async Task A_Restate_Whose_Render_Fails_Leaves_The_Issued_Document_In_Place()
    {
        var fixture = new RenderFixture();
        var order = BuildOrder(PaymentStatus.Pending);
        var receipt = BuildReceipt();
        await fixture.Service().RealizeFiscalAndPdfAsync(order, receipt, CancellationToken.None);

        order.MarkCashCollected(EmployeeId);
        fixture.RenderFails();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service().RegenerateReceiptPdfAsync(order, receipt, CancellationToken.None));

        Assert.Equal(nameof(PaymentStatus.Pending), fixture.StoredDocument(BlobName));
    }

    /// <summary>
    /// A restated document keeps the date it was issued on. It printed the render's date, so the same
    /// number came back carrying the day the cash was collected.
    /// </summary>
    [Fact]
    public async Task The_Restated_Document_Keeps_Its_Issue_Date()
    {
        var fixture = new RenderFixture();
        var receipt = BuildReceipt();
        typeof(OrderReceipt).GetProperty(nameof(OrderReceipt.IssuedAt))!
            .SetValue(receipt, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc));

        await fixture.Service().RegenerateReceiptPdfAsync(
            BuildOrder(PaymentStatus.Paid), receipt, CancellationToken.None);

        Assert.Equal("01.09.2026", fixture.LastRendered!.IssuedDate);
    }

    /// <summary>
    /// The date is the market's calendar day, not UTC's: a Prague receipt issued at 00:30 local time was
    /// dated the previous day.
    /// </summary>
    [Fact]
    public async Task A_Receipt_Issued_Just_After_Local_Midnight_Carries_The_Local_Day()
    {
        var fixture = new RenderFixture();
        fixture.MarketZone("Europe/Prague");
        var receipt = BuildReceipt();
        typeof(OrderReceipt).GetProperty(nameof(OrderReceipt.IssuedAt))!
            .SetValue(receipt, new DateTime(2026, 9, 1, 22, 30, 0, DateTimeKind.Utc));

        await fixture.Service().RegenerateReceiptPdfAsync(
            BuildOrder(PaymentStatus.Paid), receipt, CancellationToken.None);

        Assert.Equal("02.09.2026", fixture.LastRendered!.IssuedDate);
    }

    /// <summary>
    /// And it is still the customer's document: the language is read back from the receipt row, not
    /// from whoever triggered the restate — here a cleaner, whose language nobody asked for.
    /// </summary>
    [Fact]
    public async Task The_Restated_Document_Keeps_The_Customers_Language()
    {
        var fixture = new RenderFixture();

        await fixture.Service().RegenerateReceiptPdfAsync(
            BuildOrder(PaymentStatus.Paid), BuildReceipt(), CancellationToken.None);

        Assert.Equal("cs", fixture.LastRendered!.LanguageCode);
    }

    /// <summary>
    /// The first issue reads the same row. It used to take a language parameter it never read.
    /// </summary>
    [Fact]
    public async Task The_Issued_Document_Is_Written_In_The_Receipts_Language()
    {
        var fixture = new RenderFixture();

        await fixture.Service().RealizeFiscalAndPdfAsync(
            BuildOrder(PaymentStatus.Pending), BuildReceipt(), CancellationToken.None);

        Assert.Equal("cs", fixture.LastRendered!.LanguageCode);
    }

    // ── fixtures ────────────────────────────────────────────────────────────

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();

    private MarkCashCollected.Handler CreateHandler(IPendingDispatch pending)
    {
        _accessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);

        return new MarkCashCollected.Handler(
            _orderRepository.Object,
            _accessService.Object,
            Mock.Of<IStripeClient>(),
            pending,
            NullLogger<MarkCashCollected.Handler>.Instance);
    }

    private Order ArrangeOrder(bool withReceipt)
    {
        var order = BuildOrder(PaymentStatus.Pending);
        if (withReceipt)
        {
            AttachReceipt(order, BuildReceipt());
        }

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private static GenerateReceiptHandler CreateQueueHandler(
        Mock<IOrderRepository> orderRepository,
        IReceiptService receiptService,
        Mock<IEmailService> emailService) => new(
        orderRepository.Object,
        receiptService,
        emailService.Object,
        TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
        Mock.Of<ICountryConfigurationRepository>(),
        Mock.Of<IUnitOfWork>(u =>
            u.BeginTransactionAsync(It.IsAny<CancellationToken>()) == Task.FromResult(Mock.Of<IDbContextTransaction>())),
        Mock.Of<ITenantProvider>(),
        new ArchivedCompanyDeadLetter(Mock.Of<IServiceScopeFactory>(), NullLogger<ArchivedCompanyDeadLetter>.Instance),
        NullLogger<GenerateReceiptHandler>.Instance);

    private static Order BuildOrder(PaymentStatus paymentStatus)
    {
        var order = Order.Create(
            customerName: "Jan Novák",
            customerEmail: "jan@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Hlavní 2", "Praha", "11000", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: paymentStatus);
        order.Id = OrderId;
        order.SetCurrency(Czk());
        return order;
    }

    private static Currency Czk()
    {
        var czk = Currency.Create("CZK", "Kč", "Czech Koruna");
        czk.Id = "czk";
        return czk;
    }

    private static OrderReceipt BuildReceipt() =>
        OrderReceipt.Create(OrderId, ReceiptNumber, "receipt.pdf", BlobName, LanguageId);

    private static void AttachReceipt(Order order, OrderReceipt receipt) =>
        typeof(Order).GetProperty(nameof(Order.Receipt))!.SetValue(order, receipt);

    private static string ReissueBody() => JsonSerializer.Serialize(
        new QueueEnvelope<GenerateReceiptMessage>(
            MessageKeys.ReceiptReissue(OrderId),
            null,
            new GenerateReceiptMessage(OrderId, LanguageCode: string.Empty, Reissue: true)),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private sealed class RecordingPendingDispatch : IPendingDispatch
    {
        public List<(string QueueName, string MessageKey, GenerateReceiptMessage Message)> Messages { get; } = [];

        public void Enqueue<T>(string queueName, T message, string messageKey)
        {
            var envelope = Assert.IsType<QueueEnvelope<GenerateReceiptMessage>>(message);
            Messages.Add((queueName, messageKey, envelope.Payload));
        }

        public IReadOnlyList<PendingMessage> Drain() => [];
    }

    /// <summary>
    /// A real <see cref="ReceiptService"/> over doubles, and a blob store that behaves like Azure's: the
    /// stream upload creates only and refuses a name that exists; opening the writer re-creates the blob
    /// empty, and what was written lands when it closes.
    /// </summary>
    private sealed class RenderFixture
    {
        private readonly Mock<IPdfService> _pdfService = new();
        private readonly Mock<IBlobContainerClient> _blobClient = new();
        private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
        private readonly Mock<ILanguageRepository> _languageRepository = new();
        private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
        private readonly Mock<ICountryRepository> _countryRepository = new();
        private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
        private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

        public ReceiptPdfData? LastRendered { get; private set; }

        public Mock<IFiscalCounterRepository> FiscalCounters { get; } = new();

        public IReadOnlyList<string> StoredBlobNames => _blobs.Keys.ToList();

        public string StoredDocument(string blobName) => Encoding.UTF8.GetString(_blobs[blobName]);

        public void MarketZone(string timeZoneId) =>
            _countryConfigurationRepository
                .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CountryConfiguration.Create(CountryId, "czk", "cs", standardVatRate: 21m, timeZoneId: timeZoneId));

        public void RenderFails() =>
            _pdfService
                .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
                .Throws(new InvalidOperationException("render failed"));

        public RenderFixture()
        {
            // The rendered "document" is the payment status it states, so the store shows which it holds.
            _pdfService
                .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
                .Callback<ReceiptPdfData, string?>((data, _) => LastRendered = data)
                .Returns<ReceiptPdfData, string?>((data, _) => Encoding.UTF8.GetBytes(data.PaymentStatus.ToString()));

            _blobClient
                .Setup(b => b.UploadAsync(
                    It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
                .Returns<string, Stream, Metadata?, CancellationToken>((name, content, _, _) =>
                {
                    if (_blobs.ContainsKey(name))
                    {
                        throw new InvalidOperationException($"409 BlobAlreadyExists: {name}");
                    }

                    using var copy = new MemoryStream();
                    content.CopyTo(copy);
                    _blobs[name] = copy.ToArray();
                    return Task.CompletedTask;
                });
            _blobClient
                .Setup(b => b.CreateFileForWritingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string name, CancellationToken _) =>
                {
                    _blobs[name] = [];
                    return (Stream)new StoredOnDispose(bytes => _blobs[name] = bytes);
                });
            _blobClientFactory
                .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
                .Returns(_blobClient.Object);

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
            _companyInfoRepository
                .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(company);
            _countryRepository
                .Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Country.Create("Czechia", "CZE", "CZ"));
            _countryConfigurationRepository
                .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CountryConfiguration.Create(CountryId, "czk", "cs", standardVatRate: 21m));
        }

        public ReceiptService Service() => new(
            _pdfService.Object,
            Mock.Of<IOrderReceiptRepository>(),
            FiscalCounters.Object,
            _languageRepository.Object,
            _companyInfoRepository.Object,
            _countryRepository.Object,
            _countryConfigurationRepository.Object,
            _blobClientFactory.Object,
            Mock.Of<IFiscalServiceResolver>(),
            NullLogger<ReceiptService>.Instance);
    }

    /// <summary>A blob writer: what was written lands in the store when the stream is closed.</summary>
    private sealed class StoredOnDispose(Action<byte[]> store) : MemoryStream
    {
        private bool _stored;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_stored)
            {
                _stored = true;
                store(ToArray());
            }

            base.Dispose(disposing);
        }
    }
}
