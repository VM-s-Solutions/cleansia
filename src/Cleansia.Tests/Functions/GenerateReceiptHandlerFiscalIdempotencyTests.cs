using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// T-0119 (F4) / ADR-0004 — the FISCAL half of receipt idempotency (the email half is T-0118).
/// Extends TC-IDEMP-0 with the claim-before-register contract: the durable claim (the committed
/// <see cref="OrderReceipt"/> row carrying the allocated sequence) MUST commit BEFORE the
/// irreversible external effect (<see cref="IFiscalService.RegisterReceiptAsync"/>) and the PDF.
///
/// <para>The consumer is restructured into reserve → commit-claim → realize(register+PDF) → email.
/// A redelivery after the claim sees <c>order.Receipt is not null</c> and never re-burns a sequence
/// nor re-registers (AC-F4.1/AC-F4.2). Two concurrent first-deliveries collapse on the existing
/// unique index — the loser's PG 23505 (on EITHER <c>IX_OrderReceipts_OrderId</c> OR
/// <c>IX_OrderReceipts_ReceiptNumber</c>) is caught and ACKED, not thrown (AC-F4.3). The D3.3 fiscal
/// carve-out classification is preserved (AC-F4.5).</para>
///
/// <para>Written TEST-FIRST (RED on the pre-split handler, which calls the combined
/// <c>GenerateReceiptAsync</c> — registering with the authority BEFORE the row commits — so a
/// crash-then-redeliver re-registers).</para>
/// </summary>
public class GenerateReceiptHandlerFiscalIdempotencyTests
{
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

    private static void AttachReceipt(Order order, OrderReceipt receipt) =>
        typeof(Order).GetProperty(nameof(Order.Receipt))!.SetValue(order, receipt);

    private static string SerializeEnvelope(QueueEnvelope<GenerateReceiptMessage> envelope) =>
        JsonSerializer.Serialize(envelope,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static string Body() => SerializeEnvelope(new QueueEnvelope<GenerateReceiptMessage>(
        MessageKey: MessageKeys.Receipt(OrderId),
        TenantId: null,
        Payload: new GenerateReceiptMessage(OrderId, LanguageCode)));

    /// <summary>
    /// Wires the split contract for the happy path: reserve returns the staged receipt; the
    /// claim-commit makes that receipt visible on the order (so the redelivery's
    /// <c>order.Receipt is not null</c> guard fires); realize calls the fiscal authority exactly once
    /// and stamps the code; PDF download + email succeed. Models the durable-claim-before-register
    /// ordering: the commit that attaches the receipt runs BEFORE realize.
    /// </summary>
    private (Order order, OrderReceipt receipt) ArrangeHappyPath()
    {
        var order = BuildEligibleCashOrder();
        var receipt = BuildReceipt();

        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _receiptService
            .Setup(s => s.ReserveReceiptAsync(order, LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);

        // The claim commit makes the receipt durable (and visible to the redelivery guard).
        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => AttachReceipt(order, receipt))
            .Returns(Task.CompletedTask);

        // Realize stamps the fiscal code on success (mirrors SetFiscalData clearing retry-eligibility).
        _receiptService
            .Setup(s => s.RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, It.IsAny<CancellationToken>()))
            .Callback(() => receipt.SetFiscalData("cz-eet2", "FIK-123", DateTime.UtcNow))
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

    // ── AC-F4.1 (the gate) — twice-invocation → reserve + register + email each EXACTLY ONCE ──

    [Fact]
    public async Task AC_F4_1_Twice_With_Same_Envelope_Reserves_Registers_And_Emails_Exactly_Once()
    {
        ArrangeHappyPath();
        var handler = CreateHandler();
        var body = Body();

        await handler.HandleAsync(body, CancellationToken.None);
        await handler.HandleAsync(body, CancellationToken.None);

        _receiptService.Verify(
            s => s.ReserveReceiptAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _receiptService.Verify(
            s => s.RealizeFiscalAndPdfAsync(It.IsAny<Order>(), It.IsAny<OrderReceipt>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── AC-F4.1/D-F4.3 — the claim commit PRECEDES the realize/register step (claim-before-register) ──

    [Fact]
    public async Task AC_F4_1_Claim_Commit_Precedes_Realize_Register()
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
            .Setup(s => s.DownloadReceiptPdfAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);

        var committed = false;
        var realizedBeforeClaimCommit = false;

        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => committed = true)
            .Returns(Task.CompletedTask);

        _receiptService
            .Setup(s => s.RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // Realize (which performs the irreversible authority register) must NOT run before the
                // durable claim has committed.
                if (!committed)
                {
                    realizedBeforeClaimCommit = true;
                }
                receipt.SetFiscalData("cz-eet2", "FIK-123", DateTime.UtcNow);
            })
            .Returns(Task.CompletedTask);

        _emailService
            .Setup(s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("email-msg-id");

        var handler = CreateHandler();
        await handler.HandleAsync(Body(), CancellationToken.None);

        Assert.False(realizedBeforeClaimCommit,
            "the durable claim commit MUST precede RealizeFiscalAndPdfAsync (claim-before-register)");
    }

    // ── AC-F4.2 — crash AFTER register but the claim already committed → redeliver → NO re-register ──

    [Fact]
    public async Task AC_F4_2_Crash_After_Register_But_Claim_Committed_Does_Not_Re_Register()
    {
        var order = BuildEligibleCashOrder();
        var receipt = BuildReceipt();

        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _receiptService
            .Setup(s => s.ReserveReceiptAsync(order, LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);

        // The claim commit makes the receipt durable + visible to the redelivery guard.
        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => AttachReceipt(order, receipt))
            .Returns(Task.CompletedTask);

        _receiptService
            .Setup(s => s.DownloadReceiptPdfAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);

        // First delivery: register succeeds, then we simulate a crash by throwing right after.
        var firstRealizeDone = false;
        _receiptService
            .Setup(s => s.RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                receipt.SetFiscalData("cz-eet2", "FIK-123", DateTime.UtcNow);
                firstRealizeDone = true;
            })
            .Returns(() => firstRealizeDone
                ? throw new InvalidOperationException("crash after register returns, before email-commit")
                : Task.CompletedTask);

        _emailService
            .Setup(s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("email-msg-id");

        var handler = CreateHandler();
        var body = Body();

        // First delivery crashes after the register (but after the claim committed).
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(body, CancellationToken.None));

        // Redelivery: the committed claim short-circuits at the `order.Receipt is not null` guard.
        await handler.HandleAsync(body, CancellationToken.None);

        _receiptService.Verify(
            s => s.RealizeFiscalAndPdfAsync(It.IsAny<Order>(), It.IsAny<OrderReceipt>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── AC-F4.2 (inverse) — crash BEFORE the claim commit → redeliver → EXACTLY ONE register total ──

    [Fact]
    public async Task AC_F4_2_Crash_Before_Claim_Commit_Yields_Exactly_One_Register()
    {
        var order = BuildEligibleCashOrder();
        var receipt = BuildReceipt();

        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _receiptService
            .Setup(s => s.ReserveReceiptAsync(order, LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);

        // First commit (the claim) CRASHES — nothing is durable, the order keeps Receipt == null.
        var commitCount = 0;
        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                commitCount++;
                if (commitCount == 1)
                {
                    throw new InvalidOperationException("crash during the claim commit (nothing durable yet)");
                }
                AttachReceipt(order, receipt);
                return Task.CompletedTask;
            });

        _receiptService
            .Setup(s => s.RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, It.IsAny<CancellationToken>()))
            .Callback(() => receipt.SetFiscalData("cz-eet2", "FIK-123", DateTime.UtcNow))
            .Returns(Task.CompletedTask);
        _receiptService
            .Setup(s => s.DownloadReceiptPdfAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        _emailService
            .Setup(s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("email-msg-id");

        var handler = CreateHandler();
        var body = Body();

        // First delivery crashes BEFORE the claim is durable → no register happened yet.
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(body, CancellationToken.None));
        _receiptService.Verify(
            s => s.RealizeFiscalAndPdfAsync(It.IsAny<Order>(), It.IsAny<OrderReceipt>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Redelivery completes: exactly one register total across both deliveries.
        await handler.HandleAsync(body, CancellationToken.None);
        _receiptService.Verify(
            s => s.RealizeFiscalAndPdfAsync(It.IsAny<Order>(), It.IsAny<OrderReceipt>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── AC-F4.3 — concurrent first-delivery loser's 23505 (either unique index) is CAUGHT and ACKED ──

    [Fact]
    public async Task AC_F4_3_Concurrent_Loser_23505_On_OrderId_Index_Is_Acked_Not_Thrown()
    {
        await AssertLoserUniqueViolationIsAcked(MakeUniqueViolation("IX_OrderReceipts_OrderId"));
    }

    [Fact]
    public async Task AC_F4_3_Concurrent_Loser_23505_On_ReceiptNumber_Index_Is_Acked_Not_Thrown()
    {
        await AssertLoserUniqueViolationIsAcked(MakeUniqueViolation("IX_OrderReceipts_ReceiptNumber"));
    }

    private async Task AssertLoserUniqueViolationIsAcked(DbUpdateException violation)
    {
        var order = BuildEligibleCashOrder();
        var receipt = BuildReceipt();

        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _receiptService
            .Setup(s => s.ReserveReceiptAsync(order, LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);

        // The loser's claim commit collides on the existing unique index → PG 23505.
        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(violation);

        var handler = CreateHandler();

        // Already-claimed collapse: the loser must ACK (no throw, no poison loop).
        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(Body(), CancellationToken.None));
        Assert.Null(ex);

        // The loser never registers nor emails — exactly one of each happens (on the winner).
        _receiptService.Verify(
            s => s.RealizeFiscalAndPdfAsync(It.IsAny<Order>(), It.IsAny<OrderReceipt>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── AC-F4.5 — D3.3 classification preserved: target-not-found THROWS (transient); malformed ACKS ──

    [Fact]
    public async Task AC_F4_5_TargetNotFound_Still_Throws_Transient()
    {
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(Body(), CancellationToken.None));
    }

    [Fact]
    public async Task AC_F4_5_Malformed_Body_Still_Acks()
    {
        var handler = CreateHandler();
        // Invalid (non-ULID) OrderId — malformed → ack/return, no throw, no order lookup.
        var body = SerializeEnvelope(new QueueEnvelope<GenerateReceiptMessage>(
            MessageKey: MessageKeys.Receipt("not-a-ulid"),
            TenantId: null,
            Payload: new GenerateReceiptMessage("not-a-ulid", LanguageCode)));

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(body, CancellationToken.None));
        Assert.Null(ex);
        _orderRepository.Verify(
            r => r.GetByIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Builds a <see cref="DbUpdateException"/> whose inner exception exposes a public string
    /// <c>SqlState</c> = "23505" (a Postgres unique-violation), mirroring how Npgsql's
    /// <c>PostgresException</c> surfaces it. The handler detects the violation provider-agnostically by
    /// duck-typing <c>SqlState</c> (the same idiom as T-0111/T-0112/T-0114), so a faux exception with a
    /// matching <c>SqlState</c> property is sufficient to exercise the catch on EITHER unique index.
    /// </summary>
    private static DbUpdateException MakeUniqueViolation(string constraintName) =>
        new("claim insert collided with the unique index", new FakePostgresException("23505", constraintName));

    private sealed class FakePostgresException(string sqlState, string constraintName) : Exception
    {
        public string SqlState { get; } = sqlState;
        public string ConstraintName { get; } = constraintName;
    }
}
