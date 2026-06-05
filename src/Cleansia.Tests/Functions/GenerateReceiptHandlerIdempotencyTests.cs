using System.Reflection;
using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// TC-IDEMP-0 (ADR-0002 D2.1a dual-read + D2.2 claim-first receipt-email close) for the
/// <c>generate-receipt</c> consumer. Written
/// test-first (RED on the pre-fix handler, which re-sends the email on a redelivery and silently
/// discards an envelope-wrapped body).
///
/// <para><b>TC-IDEMP-0 ("safe to run twice"):</b> invoking the consumer TWICE with the SAME
/// <see cref="QueueEnvelope{T}"/> realizes the receipt creation AND the order-receipt email (the
/// terminal effect) EXACTLY ONCE. The redelivery is short-circuited by the receipt-creation guard,
/// which is load-bearing precisely because the claim (the committed receipt row) now PRECEDES the
/// email send (claim-first).</para>
///
/// <para><b>D2.1a dual-read:</b> the consumer reads the wire body either as the new
/// <see cref="QueueEnvelope{T}"/> (camelCase <c>{"messageKey","tenantId","payload":{...}}</c>) OR,
/// at the deploy boundary, as a bare <see cref="GenerateReceiptMessage"/> — synthesizing the
/// deterministic key from the payload so in-flight bare messages do not poison.</para>
///
/// <para><b>D2.2 claim-first:</b> the email-sent state is committed in a transaction that
/// PRECEDES the send, so a crash/redelivery after the send does NOT re-send. The fiscal
/// "target not found" path stays transient (covered by <see cref="TargetNotFound_Stays_Transient_Throws"/>).</para>
/// </summary>
public class GenerateReceiptHandlerIdempotencyTests
{
    // A syntactically valid ULID (26 chars, Crockford base32 — excludes I, L, O, U) so the
    // consumer's ULID guard passes.
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";
    private const string LanguageCode = "en";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IReceiptService> _receiptService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    private GenerateReceiptHandler CreateHandler() => new(
        _orderRepository.Object,
        _receiptService.Object,
        _emailService.Object,
        _countryConfigurationRepository.Object,
        _unitOfWork.Object,
        _tenantProvider.Object,
        NullLogger<GenerateReceiptHandler>.Instance);

    private static Order BuildEligibleCashOrder()
    {
        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending);
        order.Id = OrderId;
        return order;
    }

    private static OrderReceipt BuildReceipt() =>
        OrderReceipt.Create(OrderId, "2026-000001", "receipt.pdf", "2026/ORD/receipt.pdf", LanguageCode);

    // The Order.Receipt navigation has a private setter; on a real redelivery EF rehydrates it from
    // the committed row. The test simulates that committed state by attaching the receipt to the
    // SAME order instance the repository hands back on the second lookup.
    private static void AttachReceipt(Order order, OrderReceipt receipt) =>
        typeof(Order).GetProperty(nameof(Order.Receipt))!
            .SetValue(order, receipt);

    private static string SerializeEnvelope(QueueEnvelope<GenerateReceiptMessage> envelope) =>
        JsonSerializer.Serialize(envelope,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static string SerializeBare(GenerateReceiptMessage message) =>
        JsonSerializer.Serialize(message,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    /// <summary>
    /// Wires the mocks so the SAME order instance is returned on every lookup, the receipt is created
    /// once, and — crucially — the committed receipt becomes visible (attached to the order) once the
    /// claim-first commit has run, so the redelivery's creation guard fires.
    /// </summary>
    private (Order order, OrderReceipt receipt) ArrangeHappyPath()
    {
        var order = BuildEligibleCashOrder();
        var receipt = BuildReceipt();

        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // ADR-0004 — receipt creation is now the RESERVE phase (allocate + stage the row).
        _receiptService
            .Setup(s => s.ReserveReceiptAsync(order, LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);

        // Realize the fiscal register + PDF for the now-claimed receipt (CZ today = None mode no-op).
        _receiptService
            .Setup(s => s.RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Claim-first: the receipt row is durable after the commit that PRECEDES the send. Model the
        // committed state by attaching the receipt to the order on commit, so the second invocation's
        // `order.Receipt is not null` guard short-circuits before any re-send.
        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => AttachReceipt(order, receipt))
            .Returns(Task.CompletedTask);

        _receiptService
            .Setup(s => s.DownloadReceiptPdfAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);

        _emailService
            .Setup(s => s.SendOrderReceiptEmailAsync(
                order.CustomerEmail, order, It.IsAny<byte[]?>(), It.IsAny<string>(),
                LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync("email-msg-id");

        return (order, receipt);
    }

    [Fact]
    public async Task Twice_With_Same_Envelope_Generates_Receipt_And_Sends_Email_Exactly_Once()
    {
        ArrangeHappyPath();
        var handler = CreateHandler();
        var body = SerializeEnvelope(new QueueEnvelope<GenerateReceiptMessage>(
            MessageKey: MessageKeys.Receipt(OrderId),
            TenantId: null,
            Payload: new GenerateReceiptMessage(OrderId, LanguageCode)));

        await handler.HandleAsync(body, CancellationToken.None);
        await handler.HandleAsync(body, CancellationToken.None);

        _receiptService.Verify(
            s => s.ReserveReceiptAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DualRead_Bare_Payload_Is_Processed_Not_Poisoned()
    {
        ArrangeHappyPath();
        var handler = CreateHandler();

        // A bare (pre-envelope) message in-flight at the deploy boundary must still be processed.
        var body = SerializeBare(new GenerateReceiptMessage(OrderId, LanguageCode));

        await handler.HandleAsync(body, CancellationToken.None);

        _receiptService.Verify(
            s => s.ReserveReceiptAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DualRead_Envelope_Payload_Is_Processed()
    {
        ArrangeHappyPath();
        var handler = CreateHandler();
        var body = SerializeEnvelope(new QueueEnvelope<GenerateReceiptMessage>(
            MessageKey: MessageKeys.Receipt(OrderId),
            TenantId: null,
            Payload: new GenerateReceiptMessage(OrderId, LanguageCode)));

        await handler.HandleAsync(body, CancellationToken.None);

        _orderRepository.Verify(
            r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()),
            Times.Once);
        _receiptService.Verify(
            s => s.ReserveReceiptAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClaimFirst_Commits_Receipt_Before_Sending_Email()
    {
        var order = BuildEligibleCashOrder();
        var receipt = BuildReceipt();
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _receiptService
            .Setup(s => s.ReserveReceiptAsync(order, LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        _receiptService
            .Setup(s => s.RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _receiptService
            .Setup(s => s.DownloadReceiptPdfAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);

        var emailSent = false;
        var committedBeforeEmail = false;

        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // The first commit (the claim) must land BEFORE the terminal email effect.
                if (!emailSent)
                {
                    committedBeforeEmail = true;
                }
            })
            .Returns(Task.CompletedTask);

        _emailService
            .Setup(s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => emailSent = true)
            .ReturnsAsync("email-msg-id");

        var handler = CreateHandler();
        var body = SerializeEnvelope(new QueueEnvelope<GenerateReceiptMessage>(
            MessageKey: MessageKeys.Receipt(OrderId),
            TenantId: null,
            Payload: new GenerateReceiptMessage(OrderId, LanguageCode)));

        await handler.HandleAsync(body, CancellationToken.None);

        Assert.True(committedBeforeEmail, "the receipt-claim commit must precede the email send (claim-first)");
        _emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TargetNotFound_Stays_Transient_Throws()
    {
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        var handler = CreateHandler();
        var body = SerializeEnvelope(new QueueEnvelope<GenerateReceiptMessage>(
            MessageKey: MessageKeys.Receipt(OrderId),
            TenantId: null,
            Payload: new GenerateReceiptMessage(OrderId, LanguageCode)));

        // The fiscal "target not found" path stays transient/bounded-retry (D3.3): it THROWS so the
        // queue retries — it is NOT reclassified to a permanent ack.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(body, CancellationToken.None));
    }
}
