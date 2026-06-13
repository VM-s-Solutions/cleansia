using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
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
/// Fiscal-mode characterization MATRIX, the RETRY release branch
/// (<see cref="FiscalRetryService.ProcessDueRetriesAsync"/>). Pins the current behaviour:
/// after a successful fiscal retry on a still-unsent receipt, the held confirmation email is RELEASED
/// only for blocking countries (BlockingOnline / BlockingWithOfflineCache) — for None / AsyncBackground
/// the email was already delivered up front, so the retry must NOT re-send it. A null/unknown country
/// resolves to None (no release). A failed retry releases nothing for any mode.
///
/// Read-only characterization net; no retry/fiscal production code is modified.
/// </summary>
public sealed class FiscalRetryServiceModeReleaseMatrixTests
{
    private const string CountryId = "de";
    private const string LanguageCode = "en";

    private readonly Mock<IOrderReceiptRepository> _receiptRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IReceiptService> _receiptService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    public FiscalRetryServiceModeReleaseMatrixTests()
    {
        _receiptService
            .Setup(s => s.DownloadReceiptPdfAsync(It.IsAny<OrderReceipt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        _emailService
            .Setup(s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-1");
    }

    private FiscalRetryService CreateService() => new(
        _receiptRepository.Object,
        _orderRepository.Object,
        _countryConfigurationRepository.Object,
        _receiptService.Object,
        _emailService.Object,
        new NoopUnitOfWork(),
        _tenantProvider.Object,
        NullLogger<FiscalRetryService>.Instance);

    private void WireCountryConfig(FiscalEnforcementMode mode) =>
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration
                .Create(CountryId, "EUR", LanguageCode, standardVatRate: 19m)
                .UpdateFiscalEnforcementMode(mode));

    private void WireRetryResult(bool succeeded) =>
        _receiptService
            .Setup(s => s.RetryFiscalRegistrationAsync(It.IsAny<OrderReceipt>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns((OrderReceipt receipt, Order _, CancellationToken __) =>
            {
                if (succeeded)
                {
                    receipt.SetFiscalData("de-tse", $"SIG-{receipt.ReceiptNumber}", DateTime.UtcNow);
                }

                return Task.FromResult(succeeded);
            });

    private void WireDueReceiptAndOrder(OrderReceipt receipt, string? countryId)
    {
        _receiptRepository
            .Setup(r => r.GetDueForRetryAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([receipt]);
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(receipt.OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOrder(receipt.OrderId, countryId));
    }

    private static Order BuildOrder(string orderId, string? countryId)
    {
        var address = Address.Create("Hauptstr. 2", "Berlin", "10115", countryId!);
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

    private static OrderReceipt BuildDueUnsentReceipt()
    {
        var receipt = OrderReceipt.Create(
            "01HZX9N6M7Q8R9S0T1V2W3X410", "2026-000010", "2026-000010.pdf", "2026/ord/2026-000010.pdf", LanguageCode);
        receipt.ScheduleImmediateFiscalRetry();
        return receipt;
    }

    private void VerifyEmailSent(Times times) =>
        _emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times);

    // ── AC7 — successful retry RELEASES the held email only for blocking modes ──

    [Theory]
    [InlineData(FiscalEnforcementMode.BlockingOnline)]
    [InlineData(FiscalEnforcementMode.BlockingWithOfflineCache)]
    public async Task Successful_Retry_In_Blocking_Mode_Releases_The_Held_Email(FiscalEnforcementMode mode)
    {
        WireCountryConfig(mode);
        WireRetryResult(succeeded: true);
        var receipt = BuildDueUnsentReceipt();
        WireDueReceiptAndOrder(receipt, CountryId);

        await CreateService().ProcessDueRetriesAsync(CancellationToken.None);

        VerifyEmailSent(Times.Once());
        Assert.True(receipt.EmailSent);
    }

    [Theory]
    [InlineData(FiscalEnforcementMode.None)]
    [InlineData(FiscalEnforcementMode.AsyncBackground)]
    public async Task Successful_Retry_In_NonBlocking_Mode_Does_Not_Resend_The_Email(FiscalEnforcementMode mode)
    {
        WireCountryConfig(mode);
        WireRetryResult(succeeded: true);
        var receipt = BuildDueUnsentReceipt();
        WireDueReceiptAndOrder(receipt, CountryId);

        await CreateService().ProcessDueRetriesAsync(CancellationToken.None);

        VerifyEmailSent(Times.Never());
        Assert.False(receipt.EmailSent);
    }

    // ── AC7 (negative) — a FAILED retry releases nothing, even in a blocking mode ──

    [Theory]
    [InlineData(FiscalEnforcementMode.BlockingOnline)]
    [InlineData(FiscalEnforcementMode.None)]
    public async Task Failed_Retry_Releases_No_Email(FiscalEnforcementMode mode)
    {
        WireCountryConfig(mode);
        WireRetryResult(succeeded: false);
        var receipt = BuildDueUnsentReceipt();
        WireDueReceiptAndOrder(receipt, CountryId);

        await CreateService().ProcessDueRetriesAsync(CancellationToken.None);

        VerifyEmailSent(Times.Never());
        Assert.False(receipt.EmailSent);
    }

    // ── AC7 (already-sent) — a successful blocking retry on an ALREADY-sent receipt does not re-send ──

    [Fact]
    public async Task Successful_Retry_When_Email_Already_Sent_Does_Not_Resend()
    {
        WireCountryConfig(FiscalEnforcementMode.BlockingOnline);
        WireRetryResult(succeeded: true);
        var receipt = BuildDueUnsentReceipt();
        receipt.MarkEmailSent("already-sent");
        WireDueReceiptAndOrder(receipt, CountryId);

        await CreateService().ProcessDueRetriesAsync(CancellationToken.None);

        VerifyEmailSent(Times.Never());
    }

    // ── AC8 — a null country falls back to None on the retry side → no release ──

    [Fact]
    public async Task Successful_Retry_With_Null_CountryId_Falls_Back_To_None_And_Does_Not_Release()
    {
        WireCountryConfig(FiscalEnforcementMode.BlockingOnline);
        WireRetryResult(succeeded: true);
        var receipt = BuildDueUnsentReceipt();
        WireDueReceiptAndOrder(receipt, countryId: null);

        await CreateService().ProcessDueRetriesAsync(CancellationToken.None);

        VerifyEmailSent(Times.Never());
        Assert.False(receipt.EmailSent);
    }

    [Fact]
    public async Task Successful_Retry_With_No_CountryConfiguration_Row_Falls_Back_To_None_And_Does_Not_Release()
    {
        // No GetByCountryIdAsync setup → repo returns null → ResolveEnforcementMode → None.
        WireRetryResult(succeeded: true);
        var receipt = BuildDueUnsentReceipt();
        WireDueReceiptAndOrder(receipt, CountryId);

        await CreateService().ProcessDueRetriesAsync(CancellationToken.None);

        VerifyEmailSent(Times.Never());
        Assert.False(receipt.EmailSent);
    }

    private sealed class NoopUnitOfWork : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public void Dispose() { }
    }
}
