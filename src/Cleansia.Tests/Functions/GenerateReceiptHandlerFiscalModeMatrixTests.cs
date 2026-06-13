using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
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
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// Fiscal-mode characterization MATRIX, the CONSUMER hold/send branch
/// (<see cref="GenerateReceiptHandler"/> phase 3). Pins the current behaviour: a blocking
/// country (BlockingOnline / BlockingWithOfflineCache) whose generated receipt has
/// <c>FiscalCode == null</c> HOLDS the confirmation email (it will be released by the retry job once the
/// authority signs); every other mode×signature combination — None, AsyncBackground, or a blocking
/// country whose receipt is already signed — sends the email exactly once and stamps
/// <see cref="OrderReceipt.MarkEmailSent"/>. A null/unknown country falls back to None and sends.
///
/// Read-only characterization net; no consumer/fiscal production code is modified.
/// </summary>
public class GenerateReceiptHandlerFiscalModeMatrixTests
{
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";
    private const string LanguageCode = "en";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IReceiptService> _receiptService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    public GenerateReceiptHandlerFiscalModeMatrixTests()
    {
        _unitOfWork
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDbContextTransaction>());
    }

    private GenerateReceiptHandler CreateHandler() => new(
        _orderRepository.Object,
        _receiptService.Object,
        _emailService.Object,
        _countryConfigurationRepository.Object,
        _unitOfWork.Object,
        _tenantProvider.Object,
        NullLogger<GenerateReceiptHandler>.Instance);

    private static Order BuildOrder(string? countryId)
    {
        var address = Address.Create("123 Main St", "Prague", "11000", countryId!);
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

    private static string Body() =>
        JsonSerializer.Serialize(
            new QueueEnvelope<GenerateReceiptMessage>(
                MessageKey: MessageKeys.Receipt(OrderId),
                TenantId: null,
                Payload: new GenerateReceiptMessage(OrderId, LanguageCode)),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private void WireCountryConfig(string countryId, FiscalEnforcementMode mode) =>
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(countryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration
                .Create(countryId, "EUR", LanguageCode, standardVatRate: 19m)
                .UpdateFiscalEnforcementMode(mode));

    /// <summary>
    /// Wires reserve → claim-commit → realize so that realize stamps a FiscalCode iff
    /// <paramref name="signed"/>. The claim commit attaches the receipt to the order (the redelivery
    /// guard); subsequent commits are no-ops.
    /// </summary>
    private (Order order, OrderReceipt receipt) ArrangeReceiptGeneration(string? countryId, bool signed)
    {
        var order = BuildOrder(countryId);
        var receipt = BuildReceipt();

        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _receiptService
            .Setup(s => s.ReserveReceiptAsync(order, LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => AttachReceipt(order, receipt))
            .Returns(Task.CompletedTask);
        _receiptService
            .Setup(s => s.RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (signed)
                {
                    receipt.SetFiscalData("de-tse", "FIK-123", DateTime.UtcNow);
                }
            })
            .Returns(Task.CompletedTask);
        _receiptService
            .Setup(s => s.DownloadReceiptPdfAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        _emailService
            .Setup(s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("email-msg-id");

        return (order, receipt);
    }

    private void VerifyEmailSent(Times times) =>
        _emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times);

    // ── AC5 — a blocking country with an UNSIGNED receipt HOLDS the email ──

    [Theory]
    [InlineData(FiscalEnforcementMode.BlockingOnline)]
    [InlineData(FiscalEnforcementMode.BlockingWithOfflineCache)]
    public async Task Blocking_Mode_With_Null_FiscalCode_Holds_The_Email(FiscalEnforcementMode mode)
    {
        WireCountryConfig("de", mode);
        var (_, receipt) = ArrangeReceiptGeneration("de", signed: false);

        var ex = await Record.ExceptionAsync(() => CreateHandler().HandleAsync(Body(), CancellationToken.None));

        Assert.Null(ex);
        VerifyEmailSent(Times.Never());
        Assert.False(receipt.EmailSent);
    }

    // ── AC6 — non-blocking modes (or signed blocking) SEND the email exactly once and mark it ──

    [Theory]
    [InlineData(FiscalEnforcementMode.None)]
    [InlineData(FiscalEnforcementMode.AsyncBackground)]
    public async Task NonBlocking_Mode_Sends_Email_Once_And_Marks_Sent(FiscalEnforcementMode mode)
    {
        WireCountryConfig("de", mode);
        // AsyncBackground may or may not be signed; either way it is not held. Use unsigned to prove
        // the send is gated on the MODE, not on the presence of a code.
        var (_, receipt) = ArrangeReceiptGeneration("de", signed: mode != FiscalEnforcementMode.None);

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        VerifyEmailSent(Times.Once());
        Assert.True(receipt.EmailSent);
        Assert.Equal("email-msg-id", receipt.EmailMessageId);
    }

    [Theory]
    [InlineData(FiscalEnforcementMode.BlockingOnline)]
    [InlineData(FiscalEnforcementMode.BlockingWithOfflineCache)]
    public async Task Blocking_Mode_With_Signed_Receipt_Sends_Email_Once(FiscalEnforcementMode mode)
    {
        WireCountryConfig("de", mode);
        var (_, receipt) = ArrangeReceiptGeneration("de", signed: true);

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        VerifyEmailSent(Times.Once());
        Assert.True(receipt.EmailSent);
    }

    // ── AC8 — fallback to None: a null/unknown country is treated as non-blocking → send ──

    [Fact]
    public async Task Null_CountryId_Falls_Back_To_None_And_Sends_The_Email()
    {
        var (_, receipt) = ArrangeReceiptGeneration(countryId: null, signed: false);

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        VerifyEmailSent(Times.Once());
        Assert.True(receipt.EmailSent);
    }

    [Fact]
    public async Task No_CountryConfiguration_Row_Falls_Back_To_None_And_Sends_The_Email()
    {
        // No GetByCountryIdAsync setup for "de" → repo returns null → ResolveEnforcementMode → None.
        var (_, receipt) = ArrangeReceiptGeneration("de", signed: false);

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        VerifyEmailSent(Times.Once());
        Assert.True(receipt.EmailSent);
    }
}
