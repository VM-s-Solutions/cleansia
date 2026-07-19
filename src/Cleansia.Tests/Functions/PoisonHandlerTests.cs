using Cleansia.Core.Queue.Abstractions;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0002 D3 — every <c>&lt;queue&gt;-poison</c>
/// consumer writes a durable dead-letter record via <see cref="IDeadLetterStore"/> and ACKS WITHOUT
/// THROWING (so it never re-poisons). The two FISCAL poison consumers (generate-receipt,
/// generate-invoice) are asserted explicitly (they MUST write the durable row carrying at least
/// the source queue name + the raw body); the other three are covered for the same no-throw + record
/// contract.
///
/// Test-first (RED until the poison handlers + <see cref="IDeadLetterStore"/> exist). The store is
/// mocked here — its DB-backed durability is exercised by <see cref="DeadLetterStorePersistenceTests"/>.
/// </summary>
public class PoisonHandlerTests
{
    private readonly Mock<IDeadLetterStore> _store = new();

    // ── the two FISCAL poison consumers MUST record the durable row (source queue + body) ──

    [Fact]
    public async Task GenerateReceiptPoison_Records_DeadLetter_With_Source_Queue_And_Body_And_Does_Not_Throw()
    {
        var handler = new GenerateReceiptPoisonHandler(
            _store.Object, NullLogger<GenerateReceiptPoisonHandler>.Instance);
        const string body = "{\"messageKey\":\"receipt:ORDER-1\",\"payload\":{\"orderId\":\"ORDER-1\"}}";

        // No throw (acks — must not re-poison).
        await handler.HandleAsync(body, CancellationToken.None);

        _store.Verify(
            s => s.RecordAsync(QueueNames.GenerateReceipt, body, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateInvoicePoison_Records_DeadLetter_With_Source_Queue_And_Body_And_Does_Not_Throw()
    {
        var handler = new GenerateInvoicePoisonHandler(
            _store.Object, NullLogger<GenerateInvoicePoisonHandler>.Instance);
        const string body = "{\"messageKey\":\"invoice:PP-1:EMP-1\",\"payload\":{\"payPeriodId\":\"PP-1\"}}";

        await handler.HandleAsync(body, CancellationToken.None);

        _store.Verify(
            s => s.RecordAsync(QueueNames.GenerateInvoice, body, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── the other three poison consumers record + ack (log+alert+store at minimum) ──

    [Fact]
    public async Task NotificationsDispatchPoison_Records_DeadLetter_And_Does_Not_Throw()
    {
        var handler = new NotificationsDispatchPoisonHandler(
            _store.Object, NullLogger<NotificationsDispatchPoisonHandler>.Instance);
        const string body = "malformed-push-body";

        await handler.HandleAsync(body, CancellationToken.None);

        _store.Verify(
            s => s.RecordAsync(QueueNames.NotificationsDispatch, body, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SitewidePromoFanoutPoison_Records_DeadLetter_And_Does_Not_Throw()
    {
        var handler = new SitewidePromoFanoutPoisonHandler(
            _store.Object, NullLogger<SitewidePromoFanoutPoisonHandler>.Instance);
        const string body = "promo-campaign-body";

        await handler.HandleAsync(body, CancellationToken.None);

        _store.Verify(
            s => s.RecordAsync(QueueNames.SitewidePromoFanout, body, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CalculateOrderPayPoison_Records_DeadLetter_And_Does_Not_Throw()
    {
        var handler = new CalculateOrderPayPoisonHandler(
            _store.Object, NullLogger<CalculateOrderPayPoisonHandler>.Instance);
        const string body = "pay-body";

        await handler.HandleAsync(body, CancellationToken.None);

        _store.Verify(
            s => s.RecordAsync(QueueNames.CalculateOrderPay, body, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LiveActivityDispatchPoison_Records_DeadLetter_And_Does_Not_Throw()
    {
        var handler = new LiveActivityDispatchPoisonHandler(
            _store.Object, NullLogger<LiveActivityDispatchPoisonHandler>.Instance);
        const string body = "{\"messageKey\":\"liveactivity:ORDER-1:end:2\",\"payload\":{\"orderId\":\"ORDER-1\"}}";

        await handler.HandleAsync(body, CancellationToken.None);

        _store.Verify(
            s => s.RecordAsync(QueueNames.LiveActivityDispatch, body, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── a poison consumer NEVER re-poisons: even if the store throws, the consumer must NOT
    //     swallow it into a silent loss... the store owns its own commit/retry, but the poison
    //     consumer's contract is "no throw on a NORMAL record". Here we assert the happy-path no-throw
    //     and the single durable write; store-failure semantics are the store's concern. ──

    [Fact]
    public async Task Fiscal_Poison_Consumer_Writes_Exactly_One_DeadLetter_Row()
    {
        var handler = new GenerateReceiptPoisonHandler(
            _store.Object, NullLogger<GenerateReceiptPoisonHandler>.Instance);

        await handler.HandleAsync("body", CancellationToken.None);

        _store.Verify(
            s => s.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
