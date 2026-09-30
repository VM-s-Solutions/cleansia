using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The booking-confirmed arm of the send-email consumer (owner ruling 2026-09-28): the discriminator routes
/// the producer's envelope to the booked e-mail, with the order's own language and the free-cancellation
/// window that applies to this customer, and the confirmation of the contract attached, dated when the
/// contract was concluded; a redelivery sends once; a booking cancelled before the message was read is not
/// confirmed to anyone.
/// </summary>
public sealed class SendEmailHandlerOrderBookedTests
{
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3BOOK";
    private const string TenantId = "cleansia-cz";

    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ICancellationPolicyResolver> _policies = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IContractConfirmationService> _confirmations = new();
    private readonly InMemoryIdempotencyGuard _guard = new();
    private static readonly byte[] ConfirmationPdf = [0x25, 0x50, 0x44, 0x46];
    private const string ConfirmationFileName = "booking-confirmation-ORD-1.pdf";

    public SendEmailHandlerOrderBookedTests()
    {
        _policies
            .Setup(p => p.ResolveForOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CancellationPolicy(4, 2, 0.25m, 0.5m, 60, OopsWindowRule.Plus));
        _confirmations
            .Setup(c => c.ForBookingAsync(It.IsAny<Order>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfirmationPdf, ConfirmationFileName));
    }

    private SendEmailHandler CreateHandler() => new(
        _emailService.Object,
        _guard,
        _tenantProvider.Object,
        Mock.Of<IPromoCodeRepository>(),
        Mock.Of<ITenantRepository>(),
        Mock.Of<ICompanyInfoRepository>(),
        NullLogger<SendEmailHandler>.Instance,
        _orders.Object,
        TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
        Mock.Of<IUnitOfWork>(),
        _policies.Object,
        Mock.Of<IReceivableRepository>(),
        _confirmations.Object,
        Mock.Of<IWorkContractAcceptanceRepository>(),
        Mock.Of<IEmployeeRepository>());

    private Order ArrangeOrder(bool cancelled = false)
    {
        var order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: "jana@example.test",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1");
        order.Id = OrderId;
        order.TenantId = TenantId;
        order.SetLanguage("cs");
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (cancelled)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        }

        _orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private static string Body(DateTimeOffset? contractConcludedOn = null) => JsonSerializer.Serialize(
        new QueueEnvelope<SendOrderBookedEmailMessage>(
            MessageKeys.OrderBookedEmail(OrderId), TenantId,
            new SendOrderBookedEmailMessage(OrderId, "en", TenantId, contractConcludedOn)),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task The_Booking_Email_Goes_Out_Once_In_The_Orders_Language_With_The_Customers_Window()
    {
        var order = ArrangeOrder();
        var handler = CreateHandler();

        await handler.HandleAsync(Body(), CancellationToken.None);
        await handler.HandleAsync(Body(), CancellationToken.None);

        _emailService.Verify(s => s.SendOrderBookedEmailAsync(
            order.CustomerEmail, It.Is<Order>(o => o.Id == OrderId), 4, "cs", It.IsAny<CancellationToken>(), null,
            ConfirmationPdf, ConfirmationFileName),
            Times.Once);
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.AtLeastOnce);
    }

    [Fact]
    public async Task The_Confirmation_Is_Dated_When_The_Producer_Says_The_Contract_Was_Concluded()
    {
        ArrangeOrder();
        var concludedOn = new DateTimeOffset(2026, 9, 29, 12, 34, 56, TimeSpan.Zero);

        await CreateHandler().HandleAsync(Body(concludedOn), CancellationToken.None);

        _confirmations.Verify(c => c.ForBookingAsync(
            It.Is<Order>(o => o.Id == OrderId), concludedOn, "cs", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Message_Enqueued_Before_The_Moment_Was_Carried_Dates_The_Confirmation_At_The_Booking()
    {
        var order = ArrangeOrder();

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        _confirmations.Verify(c => c.ForBookingAsync(
            It.IsAny<Order>(), order.CreatedOn, "cs", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Booking_Cancelled_Before_The_Message_Was_Read_Is_Not_Confirmed()
    {
        ArrangeOrder(cancelled: true);

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        _emailService.Verify(s => s.SendOrderBookedEmailAsync(
            It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
            It.IsAny<string?>(), It.IsAny<byte[]?>(), It.IsAny<string?>()), Times.Never);
    }

    private sealed class InMemoryIdempotencyGuard : IIdempotencyGuard
    {
        private readonly HashSet<string> _claimed = [];

        public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(!_claimed.Add(messageKey));

        public Task<bool> HasProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(_claimed.Contains(messageKey));

        public Task MarkProcessedAsync(string messageKey, CancellationToken ct = default)
        {
            _claimed.Add(messageKey);
            return Task.CompletedTask;
        }
    }
}
