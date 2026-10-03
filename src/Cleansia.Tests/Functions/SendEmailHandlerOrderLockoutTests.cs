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
/// The lockout arm of the send-email consumer (owner ruling 2026-09-28, decision 11): a signed-in
/// customer whose booking an administrator cancelled as a lockout gets the cancellation e-mail once, in
/// the order's language; an order not cancelled as a lockout gets none from this message.
/// </summary>
public sealed class SendEmailHandlerOrderLockoutTests
{
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3LOCK";
    private const string TenantId = "cleansia-cz";

    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly InMemoryIdempotencyGuard _guard = new();

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
        Mock.Of<ICancellationPolicyResolver>(),
        Mock.Of<IReceivableRepository>(), Mock.Of<IContractConfirmationService>(), Mock.Of<IWorkContractAcceptanceRepository>(), Mock.Of<IEmployeeRepository>());

    private Order ArrangeOrder(string? reason)
    {
        var order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: "jana@example.test",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(-1),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.TenantId = TenantId;
        order.SetLanguage("cs");
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.Cancel(DateTime.UtcNow, CancelledBy.Admin, 1m, 0m, reason);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

        _orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private static string Body() => JsonSerializer.Serialize(
        new QueueEnvelope<SendOrderLockoutEmailMessage>(
            MessageKeys.OrderLockoutEmail(OrderId), TenantId, new SendOrderLockoutEmailMessage(OrderId, "en", TenantId)),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task The_Lockout_Cancellation_Email_Goes_Out_Once_In_The_Orders_Language()
    {
        var order = ArrangeOrder(OrderCancellationReasons.CustomerLockout);
        var handler = CreateHandler();

        await handler.HandleAsync(Body(), CancellationToken.None);
        await handler.HandleAsync(Body(), CancellationToken.None);

        _emailService.Verify(s => s.SendOrderStatusUpdateEmailAsync(
            order.CustomerEmail, It.Is<Order>(o => o.Id == OrderId), "Cancelled", "cs",
            It.IsAny<CancellationToken>(), null, null), Times.Once);
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.AtLeastOnce);
    }

    [Fact]
    public async Task An_Order_Not_Cancelled_As_A_Lockout_Gets_No_Lockout_Email()
    {
        ArrangeOrder("Double booking on our side");

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        _emailService.Verify(s => s.SendOrderStatusUpdateEmailAsync(
            It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<decimal?>(), It.IsAny<string?>()), Times.Never);
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
