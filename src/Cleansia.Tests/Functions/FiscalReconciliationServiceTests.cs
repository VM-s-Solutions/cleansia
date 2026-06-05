using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// T-0122 (FISCAL-RECON) / ADR-0002 D3.4 + ADR-0004 C-B — the DISPATCH-layer reconciliation sweep:
/// the OUTER net for the at-most-once Wave-0 dispatch gap. Wave-0 dispatch (T-0118) is at-most-once —
/// a crash between the commit and the in-memory drain loses the send (NO message → no -poison → no
/// alert; the F3 poison floor only catches enqueued-and-failed-5x). For the two FISCAL queues that
/// silent loss is a lost legal/financial artifact, so D3.4 mandates a sweep that finds
/// committed-but-unrealized fiscal work and RE-ENQUEUES it through the SAME idempotent path (harmlessly
/// deduped downstream by the deterministic MessageKey + the consumer's <c>order.Receipt is not null</c>
/// guard).
///
/// <para>The sweep is a Bucket-B system-context loop (ADR-0002 D5 Bucket B carve-out), so it calls
/// <see cref="IQueueClient"/> DIRECTLY (NOT the request-scoped <c>IPendingDispatch</c>), wrapping each
/// message in the SAME <see cref="QueueEnvelope{T}"/> + frozen <see cref="MessageKeys"/> that T-0118
/// established so the re-enqueue dedups downstream.</para>
///
/// <para>Written TEST-FIRST (RED until <c>FiscalReconciliationService</c> + the two read queries
/// exist).</para>
/// </summary>
public class FiscalReconciliationServiceTests
{
    private const string CountryId = "country-cz";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IQueueClient> _queueClient = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly FakeReconciliationConfig _config = new() { ThresholdMinutes = 15, BatchSize = 50 };

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public FiscalReconciliationServiceTests()
    {
        // No candidates by default; individual tests override.
        _orderRepository
            .Setup(r => r.GetReceiptReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _payPeriodRepository
            .Setup(r => r.GetInvoiceReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        // Default: no fiscal enforcement (CZ today is None/AsyncBackground).
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CountryConfiguration?)null);
    }

    private FiscalReconciliationService CreateService() => new(
        _orderRepository.Object,
        _payPeriodRepository.Object,
        _countryConfigurationRepository.Object,
        _queueClient.Object,
        _tenantProvider.Object,
        _config,
        NullLogger<FiscalReconciliationService>.Instance);

    private static Order BuildOrder(
        string orderId,
        PaymentType paymentType = PaymentType.Cash,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        string? tenantId = null)
    {
        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
        typeof(Address).GetProperty(nameof(Address.CountryId))!.SetValue(address, CountryId);
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: paymentStatus);
        order.Id = orderId;
        order.TenantId = tenantId;
        return order;
    }

    private static QueueEnvelope<GenerateReceiptMessage>? DeserializeReceiptEnvelope(string body) =>
        JsonSerializer.Deserialize<QueueEnvelope<GenerateReceiptMessage>>(body, JsonOptions);

    private static QueueEnvelope<GenerateInvoiceMessage>? DeserializeInvoiceEnvelope(string body) =>
        JsonSerializer.Deserialize<QueueEnvelope<GenerateInvoiceMessage>>(body, JsonOptions);

    // ── AC1 — receipt recon re-enqueues a stale-missing receipt with the frozen key ──

    [Fact]
    public async Task AC1_Receipt_Recon_ReEnqueues_Stale_Missing_With_Frozen_Key()
    {
        var orderId = "01HZX9N6M7Q8R9S0T1V2W3X401";
        var order = BuildOrder(orderId, tenantId: "tenant-1");
        _orderRepository
            .Setup(r => r.GetReceiptReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);

        QueueEnvelope<GenerateReceiptMessage>? captured = null;
        _queueClient
            .Setup(q => q.SendAsync(
                QueueNames.GenerateReceipt, It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<string, QueueEnvelope<GenerateReceiptMessage>, CancellationToken>((_, env, _) => captured = env)
            .Returns(Task.CompletedTask);

        var reEnqueued = await CreateService().ReconcileAsync(CancellationToken.None);

        Assert.Equal(1, reEnqueued);
        _queueClient.Verify(
            q => q.SendAsync(
                QueueNames.GenerateReceipt, It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(MessageKeys.Receipt(orderId), captured!.MessageKey);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(orderId, captured.Payload.OrderId);
    }

    // ── AC1 — the SAME wire body the consumer dual-reads (a QueueEnvelope<GenerateReceiptMessage>) ──

    [Fact]
    public async Task AC1_Receipt_ReEnqueue_Wire_Body_Matches_The_Consumer_Envelope_Shape()
    {
        var orderId = "01HZX9N6M7Q8R9S0T1V2W3X402";
        var order = BuildOrder(orderId);
        _orderRepository
            .Setup(r => r.GetReceiptReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);

        // The Bucket-B caller hands a TYPED QueueEnvelope to IQueueClient, which serializes it camelCase
        // exactly as InMemoryPendingDispatch/AzureStorageQueueClient do — round-trip it to prove the
        // GenerateReceiptHandler dual-read will accept it.
        object? capturedMessage = null;
        _queueClient
            .Setup(q => q.SendAsync(
                QueueNames.GenerateReceipt, It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<string, QueueEnvelope<GenerateReceiptMessage>, CancellationToken>((_, env, _) => capturedMessage = env)
            .Returns(Task.CompletedTask);

        await CreateService().ReconcileAsync(CancellationToken.None);

        var body = JsonSerializer.Serialize(capturedMessage, JsonOptions);
        var roundTripped = DeserializeReceiptEnvelope(body);
        Assert.NotNull(roundTripped);
        Assert.Equal(MessageKeys.Receipt(orderId), roundTripped!.MessageKey);
        Assert.Equal(orderId, roundTripped.Payload.OrderId);
    }

    // ── AC2 — invoice recon re-enqueues a missing invoice with the frozen key ──

    [Fact]
    public async Task AC2_Invoice_Recon_ReEnqueues_Missing_With_Frozen_Key()
    {
        var payPeriodId = "period-1";
        var employeeId = "emp-1";
        _payPeriodRepository
            .Setup(r => r.GetInvoiceReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new InvoiceReconciliationItem(payPeriodId, employeeId, "tenant-9")]);

        QueueEnvelope<GenerateInvoiceMessage>? captured = null;
        _queueClient
            .Setup(q => q.SendAsync(
                QueueNames.GenerateInvoice, It.IsAny<QueueEnvelope<GenerateInvoiceMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<string, QueueEnvelope<GenerateInvoiceMessage>, CancellationToken>((_, env, _) => captured = env)
            .Returns(Task.CompletedTask);

        var reEnqueued = await CreateService().ReconcileAsync(CancellationToken.None);

        Assert.Equal(1, reEnqueued);
        _queueClient.Verify(
            q => q.SendAsync(
                QueueNames.GenerateInvoice, It.IsAny<QueueEnvelope<GenerateInvoiceMessage>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(MessageKeys.Invoice(payPeriodId, employeeId), captured!.MessageKey);
        Assert.Equal("tenant-9", captured.TenantId);
        Assert.Equal(payPeriodId, captured.Payload.PayPeriodId);
        Assert.Equal(employeeId, captured.Payload.EmployeeId);
    }

    // ── AC3 — a re-enqueue racing a success → exactly one effect via the consumer guard ──
    //
    // The sweep cannot itself realize the receipt; its only job is to re-enqueue ONE message per
    // OrderId. The "exactly one effect" guarantee comes from the deterministic key + the consumer
    // short-circuit (proven by the GenerateReceiptHandler*IdempotencyTests). Here we pin the sweep's
    // half of AC3: even if the recon query (racing a just-late dispatch) still returns the order, the
    // sweep emits exactly ONE re-enqueue carrying the deterministic receipt:{OrderId} key — so two
    // sweeps + the racing dispatch all collapse onto one key downstream (no double-realize).

    [Fact]
    public async Task AC3_ReEnqueue_Racing_A_Success_Emits_One_Deterministic_Keyed_Message()
    {
        var orderId = "01HZX9N6M7Q8R9S0T1V2W3X403";
        var order = BuildOrder(orderId);
        _orderRepository
            .Setup(r => r.GetReceiptReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);

        var keys = new List<string>();
        _queueClient
            .Setup(q => q.SendAsync(
                QueueNames.GenerateReceipt, It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<string, QueueEnvelope<GenerateReceiptMessage>, CancellationToken>((_, env, _) => keys.Add(env.MessageKey))
            .Returns(Task.CompletedTask);

        await CreateService().ReconcileAsync(CancellationToken.None);

        Assert.Single(keys);
        Assert.Equal(MessageKeys.Receipt(orderId), keys[0]);
    }

    // ── AC4 — the carve-out: the sweep does NOT swallow a "target not found" into an ack. The sweep
    //         re-enqueues only; the generate-receipt consumer's target-not-found THROW (transient,
    //         bounded retry) is untouched. We pin that the sweep never acks/short-circuits a queue
    //         send failure into success — a transient SendAsync fault must NOT be silently dropped as
    //         "done" (it would mask the silent loss this sweep exists to catch). ──

    [Fact]
    public async Task AC4_Sweep_Does_Not_Mask_A_Transient_ReEnqueue_Failure_As_Done()
    {
        var orderId = "01HZX9N6M7Q8R9S0T1V2W3X404";
        var order = BuildOrder(orderId);
        _orderRepository
            .Setup(r => r.GetReceiptReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);

        // A transient enqueue fault on this item.
        _queueClient
            .Setup(q => q.SendAsync(
                QueueNames.GenerateReceipt, It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient queue fault"));

        // The sweep must NOT count this item as re-enqueued (it stays a candidate for the next tick).
        var reEnqueued = await CreateService().ReconcileAsync(CancellationToken.None);
        Assert.Equal(0, reEnqueued);
    }

    // ── AC5 — batch-bounded per tick (passes the configured BatchSize as the take cap) ──

    [Fact]
    public async Task AC5_Sweep_Is_Batch_Bounded_By_Configured_BatchSize()
    {
        _config.BatchSize = 50;

        int? receiptTake = null;
        int? invoiceTake = null;
        _orderRepository
            .Setup(r => r.GetReceiptReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, int, CancellationToken>((_, take, _) => receiptTake = take)
            .ReturnsAsync([]);
        _payPeriodRepository
            .Setup(r => r.GetInvoiceReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, int, CancellationToken>((_, take, _) => invoiceTake = take)
            .ReturnsAsync([]);

        await CreateService().ReconcileAsync(CancellationToken.None);

        Assert.Equal(50, receiptTake);
        Assert.Equal(50, invoiceTake);
    }

    // ── AC5 — twice = once: two back-to-back sweeps re-enqueue the SAME deterministic key both times,
    //         which the downstream guard collapses to a single effect (no duplicate effect). ──

    [Fact]
    public async Task AC5_Two_Back_To_Back_Sweeps_ReEnqueue_The_Same_Deterministic_Key()
    {
        var orderId = "01HZX9N6M7Q8R9S0T1V2W3X405";
        var order = BuildOrder(orderId);
        _orderRepository
            .Setup(r => r.GetReceiptReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);

        var keys = new List<string>();
        _queueClient
            .Setup(q => q.SendAsync(
                QueueNames.GenerateReceipt, It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<string, QueueEnvelope<GenerateReceiptMessage>, CancellationToken>((_, env, _) => keys.Add(env.MessageKey))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.ReconcileAsync(CancellationToken.None);
        await service.ReconcileAsync(CancellationToken.None);

        // Two sweeps → two sends, but BOTH carry the identical frozen key → one effect downstream.
        Assert.Equal(2, keys.Count);
        Assert.All(keys, k => Assert.Equal(MessageKeys.Receipt(orderId), k));
        Assert.Single(keys.Distinct());
    }

    // ── AC6 — system context: per-item ClearTenantOverride() then SetTenantOverride(item.TenantId) ──

    [Fact]
    public async Task AC6_Per_Item_Tenant_Override_Is_Set_From_The_Item_Tenant()
    {
        var orderId = "01HZX9N6M7Q8R9S0T1V2W3X406";
        var order = BuildOrder(orderId, tenantId: "tenant-77");
        _orderRepository
            .Setup(r => r.GetReceiptReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);
        _queueClient
            .Setup(q => q.SendAsync(
                QueueNames.GenerateReceipt, It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateService().ReconcileAsync(CancellationToken.None);

        // Mirrors the FiscalRetryService.cs:42-48 system-job pattern: clear, then set per item.
        _tenantProvider.Verify(t => t.ClearTenantOverride(), Times.AtLeastOnce);
        _tenantProvider.Verify(t => t.SetTenantOverride("tenant-77"), Times.Once);
    }

    [Fact]
    public async Task AC6_Null_Tenant_Item_Does_Not_Set_Override_But_Still_Clears()
    {
        var orderId = "01HZX9N6M7Q8R9S0T1V2W3X407";
        var order = BuildOrder(orderId, tenantId: null);
        _orderRepository
            .Setup(r => r.GetReceiptReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);
        _queueClient
            .Setup(q => q.SendAsync(
                QueueNames.GenerateReceipt, It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateService().ReconcileAsync(CancellationToken.None);

        _tenantProvider.Verify(t => t.ClearTenantOverride(), Times.AtLeastOnce);
        _tenantProvider.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
    }

    // ── AC7 — the threshold is configurable: the cutoff passed to the query is now - ThresholdMinutes ──

    [Fact]
    public async Task AC7_Threshold_Cutoff_Honors_Configured_Minutes()
    {
        _config.ThresholdMinutes = 30;
        var before = DateTime.UtcNow.AddMinutes(-30);

        DateTime? cutoff = null;
        _orderRepository
            .Setup(r => r.GetReceiptReconciliationCandidatesAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, int, CancellationToken>((c, _, _) => cutoff = c)
            .ReturnsAsync([]);

        await CreateService().ReconcileAsync(CancellationToken.None);
        var after = DateTime.UtcNow.AddMinutes(-30);

        Assert.NotNull(cutoff);
        Assert.InRange(cutoff!.Value, before.AddSeconds(-5), after.AddSeconds(5));
    }

    private sealed class FakeReconciliationConfig : IFiscalReconciliationConfig
    {
        public int ThresholdMinutes { get; set; } = 15;
        public int BatchSize { get; set; } = 50;
    }
}
