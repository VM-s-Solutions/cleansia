using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// THE RECEIPT IS WRITTEN IN THE LANGUAGE THE CUSTOMER BOOKED IN, whoever asked for it.
///
/// <para>The language the document is issued in was whatever the producer put on the message, and only
/// the cash booking put the customer's there: the card webhook, the recurring confirmation, the
/// completion sweep and the fiscal reconciliation all sent English. The order now records the language
/// its booking was made in, and the consumer reads that first — then the account's preference, for an
/// occurrence no request made — and the receipt row records the result.</para>
/// </summary>
public class GenerateReceiptHandlerLanguageTests
{
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IReceiptService> _receiptService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private string? _documentLanguage;
    private string? _emailLanguage;

    public GenerateReceiptHandlerLanguageTests()
    {
        _unitOfWork
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDbContextTransaction>());
        _receiptService
            .Setup(s => s.ReserveReceiptAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Order, string, CancellationToken>((_, language, _) => _documentLanguage = language)
            .ReturnsAsync(OrderReceipt.Create(OrderId, "2026-000001", "receipt.pdf", "2026/ORD/receipt.pdf", "lang"));
        _receiptService
            .Setup(s => s.DownloadReceiptPdfAsync(It.IsAny<OrderReceipt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        _emailService
            .Setup(s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, Order, byte[]?, string, string, CancellationToken, string?>(
                (_, _, _, _, language, _, _) => _emailLanguage = language)
            .ReturnsAsync("msg-1");
    }

    /// <summary>
    /// A Google or Apple sign-up is stored as English and the web client never writes the preference
    /// back, so the account says English while the customer booked in Czech. The booking wins.
    /// </summary>
    [Fact]
    public async Task An_Account_Booked_In_Czech_Gets_A_Czech_Receipt_Whatever_Its_Stored_Preference()
    {
        await IssueAsync(OrderFor(bookedIn: "cs", accountLanguage: "en"), messageLanguage: "en");

        Assert.Equal("cs", _documentLanguage);
        Assert.Equal("cs", _emailLanguage);
    }

    /// <summary>
    /// A guest who pays by card is issued the receipt by the Stripe webhook, which passes English for
    /// everyone. The language their booking was made in is on the order.
    /// </summary>
    [Fact]
    public async Task A_Guest_Card_Order_Booked_In_Czech_Gets_A_Czech_Receipt_From_The_Webhooks_English_Message()
    {
        await IssueAsync(OrderFor(bookedIn: "cs", accountLanguage: null), messageLanguage: "en");

        Assert.Equal("cs", _documentLanguage);
        Assert.Equal("cs", _emailLanguage);
    }

    /// <summary>
    /// A recurring occurrence has no booking request of its own, so its order records no language and
    /// the account's preference decides.
    /// </summary>
    [Fact]
    public async Task An_Order_No_Request_Made_Follows_The_Accounts_Language()
    {
        await IssueAsync(OrderFor(bookedIn: null, accountLanguage: "sk"), messageLanguage: "en");

        Assert.Equal("sk", _documentLanguage);
        Assert.Equal("sk", _emailLanguage);
    }

    /// <summary>
    /// With neither on the order, the producer's language is the last word.
    /// </summary>
    [Fact]
    public async Task With_Nothing_On_The_Order_The_Producers_Language_Is_Used()
    {
        await IssueAsync(OrderFor(bookedIn: null, accountLanguage: null), messageLanguage: "uk");

        Assert.Equal("uk", _documentLanguage);
        Assert.Equal("uk", _emailLanguage);
    }

    /// <summary>
    /// A language outside the five is resolved to English before the receipt row is written: an unknown
    /// code would fail the language lookup and dead-letter the receipt.
    /// </summary>
    [Fact]
    public async Task A_Language_No_Receipt_Is_Written_In_Falls_Back_To_English()
    {
        await IssueAsync(OrderFor(bookedIn: "de", accountLanguage: "cs"), messageLanguage: "cs");

        Assert.Equal("en", _documentLanguage);
        Assert.Equal("en", _emailLanguage);
    }

    private async Task IssueAsync(Order order, string messageLanguage)
    {
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await new GenerateReceiptHandler(
                _orderRepository.Object,
                _receiptService.Object,
                _emailService.Object,
                TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
                Mock.Of<ICountryConfigurationRepository>(),
                _unitOfWork.Object,
                Mock.Of<ITenantProvider>(),
                new ArchivedCompanyDeadLetter(Mock.Of<IServiceScopeFactory>(), NullLogger<ArchivedCompanyDeadLetter>.Instance),
                NullLogger<GenerateReceiptHandler>.Instance)
            .HandleAsync(
                JsonSerializer.Serialize(
                    new QueueEnvelope<GenerateReceiptMessage>(
                        MessageKeys.Receipt(OrderId), null, new GenerateReceiptMessage(OrderId, messageLanguage)),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                CancellationToken.None);
    }

    private static Order OrderFor(string? bookedIn, string? accountLanguage)
    {
        var order = Order.Create(
            customerName: "Jan Novák",
            customerEmail: "jan@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Hlavní 2", "Praha", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: accountLanguage is null ? null : "user-1");
        order.Id = OrderId;
        order.SetLanguage(bookedIn);

        if (accountLanguage is not null)
        {
            var account = User.CreateWithPassword(
                "jan@example.com", "Secret-123", "Jan", "Novák", languageCode: accountLanguage);
            typeof(Order).GetProperty(nameof(Order.User))!.SetValue(order, account);
        }

        return order;
    }
}
