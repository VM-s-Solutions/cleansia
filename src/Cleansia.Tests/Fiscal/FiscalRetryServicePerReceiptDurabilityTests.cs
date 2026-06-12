using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Fiscal.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Fiscal;

/// <summary>
/// T-0184 / F6 — <see cref="FiscalRetryService"/> per-receipt durability + held-email at-most-once.
///
/// <para>The pre-fix service accumulated every per-receipt mutation in one <c>DbContext</c> and
/// persisted them in a single batch-wide <c>CommitAsync</c> after the loop. Two holes follow: a
/// single commit fault rolls back the retry-tracking of the WHOLE batch (no automatic re-run — it is
/// a timer), and a BlockingOnline receipt's released email is marked sent in-memory only, so a failed
/// batch commit re-sends it on the next tick (ADR-0002 D2.2 — terminal email must be idempotent /
/// claim-first).</para>
///
/// <para>Mutations are made durable per receipt (a commit inside the loop, inside the per-receipt
/// try/catch), so one faulting receipt does not roll back the already-processed ones; and the
/// <c>EmailSent</c> claim is committed BEFORE the send, so a commit fault leaves the email un-sent
/// (not sent-but-unmarked) — the next tick sends it for the first time, never twice. Written
/// TEST-FIRST (RED before the per-receipt commit + claim-first reorder).</para>
/// </summary>
public sealed class FiscalRetryServicePerReceiptDurabilityTests
{
    private const string CountryId = "de";
    private const string LanguageCode = "en";

    private readonly Mock<IOrderReceiptRepository> _receiptRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IReceiptService> _receiptService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    public FiscalRetryServicePerReceiptDurabilityTests()
    {
        var config = CountryConfiguration
            .Create(CountryId, "EUR", LanguageCode, standardVatRate: 19m)
            .UpdateFiscalEnforcementMode(FiscalEnforcementMode.BlockingOnline);
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _receiptService
            .Setup(s => s.DownloadReceiptPdfAsync(It.IsAny<OrderReceipt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);

        // Successful fiscal retry stamps the receipt and clears its retry-eligibility, mirroring the
        // real ReceiptService.RetryFiscalRegistrationAsync success path.
        _receiptService
            .Setup(s => s.RetryFiscalRegistrationAsync(It.IsAny<OrderReceipt>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns((OrderReceipt receipt, Order _, CancellationToken __) =>
            {
                receipt.SetFiscalData("de-tse-test", $"SIG-{receipt.ReceiptNumber}", DateTime.UtcNow);
                return Task.FromResult(true);
            });

        _emailService
            .Setup(s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-1");
    }

    private FiscalRetryService CreateService(IUnitOfWork unitOfWork) => new(
        _receiptRepository.Object,
        _orderRepository.Object,
        _countryConfigurationRepository.Object,
        _receiptService.Object,
        _emailService.Object,
        unitOfWork,
        _tenantProvider.Object,
        NullLogger<FiscalRetryService>.Instance);

    private static Order BuildOrder(string orderId)
    {
        var address = Address.Create("Hauptstr. 2", "Berlin", "10115", CountryId);
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+490000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "eur",
            paymentStatus: PaymentStatus.Pending);
        order.Id = orderId;
        return order;
    }

    private static OrderReceipt BuildDueReceipt(string orderId, string receiptNumber)
    {
        var receipt = OrderReceipt.Create(orderId, receiptNumber, $"{receiptNumber}.pdf", $"2026/{orderId}/{receiptNumber}.pdf", LanguageCode);
        receipt.ScheduleImmediateFiscalRetry();
        return receipt;
    }

    private void WireOrderLookups(params (string orderId, Order order)[] orders)
    {
        foreach (var (orderId, order) in orders)
        {
            _orderRepository
                .Setup(r => r.GetByIdIgnoringTenantAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
        }
    }

    // ── AC1 — a single receipt's commit fault does NOT roll back the already-processed receipts ──

    [Fact]
    public async Task AC1_One_Receipt_Commit_Fault_Does_Not_Lose_The_Already_Processed_Receipts()
    {
        // AsyncBackground — no held-email path, so each receipt has exactly ONE commit (the fiscal
        // stamp + retry-tracking), making "fault the 2nd receipt's commit" unambiguous. The held-email
        // claim-first path is exercised separately by AC2.
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration
                .Create(CountryId, "EUR", LanguageCode, standardVatRate: 19m)
                .UpdateFiscalEnforcementMode(FiscalEnforcementMode.AsyncBackground));

        var r1 = BuildDueReceipt("01HZX9N6M7Q8R9S0T1V2W3X401", "2026-000001");
        var r2 = BuildDueReceipt("01HZX9N6M7Q8R9S0T1V2W3X402", "2026-000002");
        var r3 = BuildDueReceipt("01HZX9N6M7Q8R9S0T1V2W3X403", "2026-000003");

        _receiptRepository
            .Setup(r => r.GetDueForRetryAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([r1, r2, r3]);

        WireOrderLookups(
            (r1.OrderId, BuildOrder(r1.OrderId)),
            (r2.OrderId, BuildOrder(r2.OrderId)),
            (r3.OrderId, BuildOrder(r3.OrderId)));

        // Fault ONLY the second per-receipt commit. With per-receipt commits, r1's state is durable
        // (commit #1 succeeded) and r3 is still processed (commit #3); with a single batch-wide commit
        // the whole batch is lost in the one failing commit.
        var unitOfWork = new FaultingUnitOfWork(failOnAttempt: 2);

        var processed = await CreateService(unitOfWork).ProcessDueRetriesAsync(CancellationToken.None);

        Assert.Equal(3, processed);

        // r1 and r3 committed (their fiscal stamp persisted); r2's commit threw → not committed but the
        // loop continued. Two successful commits, one fault.
        Assert.Equal(2, unitOfWork.SuccessfulCommits);
        Assert.Equal(1, unitOfWork.FailedCommits);

        // The fiscal stamp written in-memory by the mock retry survives on the committed receipts.
        Assert.NotNull(r1.FiscalCode);
        Assert.NotNull(r3.FiscalCode);
    }

    // ── AC2 — a BlockingOnline held-email is sent AT MOST ONCE across two consecutive ticks ──

    [Fact]
    public async Task AC2_Held_Email_Is_Not_Resent_After_A_Commit_Failure_On_The_Next_Tick()
    {
        var receipt = BuildDueReceipt("01HZX9N6M7Q8R9S0T1V2W3X410", "2026-000010");

        // The receipt stays "due" until its EmailSent claim is durably committed. Tick 1's claim commit
        // faults; tick 2's succeeds. The receipt is re-read both ticks (timer re-reads GetDueForRetry).
        _receiptRepository
            .Setup(r => r.GetDueForRetryAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => receipt.EmailSent ? [] : [receipt]);

        WireOrderLookups((receipt.OrderId, BuildOrder(receipt.OrderId)));

        // Claim-first ordering means the EmailSent marker is committed BEFORE the send. Faulting that
        // commit on tick 1 must leave the email UN-sent (not sent-but-unmarked) so tick 2 can send it
        // for the first time without a double-send.
        var unitOfWork = new FaultingUnitOfWork(failOnAttempt: 1);

        var service = CreateService(unitOfWork);

        await service.ProcessDueRetriesAsync(CancellationToken.None); // tick 1 — claim commit faults
        await service.ProcessDueRetriesAsync(CancellationToken.None); // tick 2 — claim commits, email sent

        _emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.True(receipt.EmailSent);
    }

    // ── AC3 — the multi-tenant override is set/cleared per receipt ──

    [Fact]
    public async Task AC3_Tenant_Override_Is_Cleared_Then_Set_Per_Receipt()
    {
        var receipt = BuildDueReceipt("01HZX9N6M7Q8R9S0T1V2W3X420", "2026-000020");
        // The receipt carries the tenant so the per-receipt override is set before any child write.
        ((ITenantEntity)receipt).TenantId = "tenant-a";

        _receiptRepository
            .Setup(r => r.GetDueForRetryAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([receipt]);

        WireOrderLookups((receipt.OrderId, BuildOrder(receipt.OrderId)));

        var unitOfWork = new FaultingUnitOfWork(failOnAttempt: 0);
        await CreateService(unitOfWork).ProcessDueRetriesAsync(CancellationToken.None);

        _tenantProvider.Verify(t => t.ClearTenantOverride(), Times.AtLeastOnce);
        _tenantProvider.Verify(t => t.SetTenantOverride("tenant-a"), Times.Once);
    }

    /// <summary>
    /// An <see cref="IUnitOfWork"/> whose Nth <c>CommitAsync</c> throws (<paramref name="failOnAttempt"/>
    /// is 1-based; 0 = never fault), simulating a per-receipt persistence fault. Counts successful and
    /// failed commits so a test can assert the loop kept committing the remaining receipts.
    /// </summary>
    private sealed class FaultingUnitOfWork(int failOnAttempt) : IUnitOfWork
    {
        private int _attempts;

        public int SuccessfulCommits { get; private set; }
        public int FailedCommits { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            _attempts++;
            if (_attempts == failOnAttempt)
            {
                FailedCommits++;
                throw new InvalidOperationException("Simulated per-receipt commit fault");
            }

            SuccessfulCommits++;
            return Task.CompletedTask;
        }

        public void Rollback() { }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Dispose() { }
    }
}
