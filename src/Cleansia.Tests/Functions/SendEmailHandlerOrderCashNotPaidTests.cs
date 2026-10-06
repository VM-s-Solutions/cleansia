using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
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
/// The cash-not-paid arm of the send-email consumer: the customer whose cleaner reported no cash at the door
/// is told once, at the order's address and in its language, under the order's company; a price paid or
/// written off before the message was read is not chased.
/// </summary>
public sealed class SendEmailHandlerOrderCashNotPaidTests
{
    private const string TenantId = "cleansia-cz";

    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly InMemoryIdempotencyGuard _guard = new();
    private readonly Order _order;
    private readonly Receivable _receivable;

    public SendEmailHandlerOrderCashNotPaidTests()
    {
        _order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: "jana@example.test",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(-3),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        _order.TenantId = TenantId;
        _order.SetLanguage("cs");
        _orders.Setup(r => r.GetQueryable()).Returns(new[] { _order }.AsQueryable().BuildMock());

        _receivable = Receivable.ForUnpaidCash(_order);
        _receivables.Setup(r => r.GetByIdAsync(_receivable.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_receivable);
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
        Mock.Of<ICancellationPolicyResolver>(),
        _receivables.Object, Mock.Of<IContractConfirmationService>(), Mock.Of<IWorkContractAcceptanceRepository>(), Mock.Of<IEmployeeRepository>());

    private string Body() => JsonSerializer.Serialize(
        new QueueEnvelope<SendOrderCashNotPaidEmailMessage>(
            MessageKeys.OrderCashNotPaidEmail(_receivable.Id),
            TenantId,
            new SendOrderCashNotPaidEmailMessage(_receivable.Id, "en", TenantId)),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task The_Customer_Is_Told_Once_At_The_Orders_Address_In_The_Orders_Language()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Body(), CancellationToken.None);
        await handler.HandleAsync(Body(), CancellationToken.None);

        _emailService.Verify(s => s.SendOrderCashNotPaidEmailAsync(
            "jana@example.test", It.Is<Order>(o => o.Id == _order.Id), _receivable, "cs", It.IsAny<CancellationToken>()),
            Times.Once);
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Price_Paid_Or_Written_Off_Before_The_Message_Was_Read_Is_Not_Chased(bool paid)
    {
        if (paid)
        {
            _receivable.MarkPaid("pi_cash_not_paid", DateTimeOffset.UtcNow);
        }
        else
        {
            _receivable.WriteOff("manager-1", "The customer paid the cleaner after all", DateTimeOffset.UtcNow);
        }

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        _emailService.VerifyNoOtherCalls();
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
