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
/// The pay-link arm of the send-email consumer: the customer whose off-session charge failed is e-mailed the
/// link, once, at the order's address and in its language; a receivable settled or written off before the
/// message was read is not chased.
/// </summary>
public sealed class SendEmailHandlerReceivablePayLinkTests
{
    private const string TenantId = "cleansia-cz";
    private const string PayUrl = "https://checkout.stripe.test/pay/link";

    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly InMemoryIdempotencyGuard _guard = new();
    private readonly Order _order;
    private readonly Receivable _receivable;

    public SendEmailHandlerReceivablePayLinkTests()
    {
        _order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: "jana@example.test",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1");
        _order.TenantId = TenantId;
        _order.SetLanguage("cs");
        _orders.Setup(r => r.GetQueryable()).Returns(new[] { _order }.AsQueryable().BuildMock());

        _receivable = Receivable.ForCashCancellationFee(_order, 375m);
        _receivable.RecordChargeAttempt();
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
        new QueueEnvelope<SendReceivablePayLinkEmailMessage>(
            MessageKeys.ReceivablePayLinkEmail(_receivable.Id, 1),
            TenantId,
            new SendReceivablePayLinkEmailMessage(_receivable.Id, 1, PayUrl, TenantId)),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task The_Pay_Link_Goes_Out_Once_To_The_Orders_Customer_In_The_Orders_Language()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Body(), CancellationToken.None);
        await handler.HandleAsync(Body(), CancellationToken.None);

        _emailService.Verify(s => s.SendReceivablePayLinkEmailAsync(
            "jana@example.test", It.Is<Order>(o => o.Id == _order.Id), _receivable, PayUrl, "cs", It.IsAny<CancellationToken>()),
            Times.Once);
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.Once);
    }

    [Fact]
    public async Task A_Receivable_Settled_Before_The_Message_Was_Read_Is_Not_Chased()
    {
        _receivable.MarkPaid("pi_link", DateTimeOffset.UtcNow);

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        _emailService.VerifyNoOtherCalls();
    }

    private sealed class InMemoryIdempotencyGuard : Cleansia.Core.Queue.Abstractions.IIdempotencyGuard
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
