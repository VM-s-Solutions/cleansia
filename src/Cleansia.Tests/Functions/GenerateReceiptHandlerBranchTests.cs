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
/// AC2 — the three GenerateReceiptHandler accept/hold branches the existing idempotency/fiscal suites
/// do not pin: an order ineligible by payment type/status is acked with no receipt generated; an order
/// that already has a receipt is acked with no re-reserve; and a BlockingOnline country whose receipt
/// has no fiscal signature HOLDS the email (commit, no send, no throw) so the retry job releases it
/// later — the customer is never blocked but the legally-required signature precedes delivery.
/// </summary>
public class GenerateReceiptHandlerBranchTests
{
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";
    private const string LanguageCode = "en";
    private const string CountryId = "de";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IReceiptService> _receiptService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    public GenerateReceiptHandlerBranchTests()
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

    private static Order BuildOrder(PaymentType paymentType, PaymentStatus paymentStatus, string countryId = "cz")
    {
        var address = Address.Create("123 Main St", "Prague", "11000", countryId);
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
        order.Id = OrderId;
        return order;
    }

    private static OrderReceipt BuildReceipt() =>
        OrderReceipt.Create(OrderId, "2026-000001", "receipt.pdf", "2026/ORD/receipt.pdf", LanguageCode);

    private static void AttachReceipt(Order order, OrderReceipt receipt) =>
        typeof(Order).GetProperty(nameof(Order.Receipt))!.SetValue(order, receipt);

    private static string Body() => JsonSerializer.Serialize(
        new QueueEnvelope<GenerateReceiptMessage>(
            MessageKeys.Receipt(OrderId), null, new GenerateReceiptMessage(OrderId, LanguageCode)),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task Ineligible_Card_Order_Not_Yet_Paid_Is_Acked_Without_Generating_A_Receipt()
    {
        var order = BuildOrder(PaymentType.Card, PaymentStatus.Pending);
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var ex = await Record.ExceptionAsync(() => CreateHandler().HandleAsync(Body(), CancellationToken.None));

        Assert.Null(ex);
        _receiptService.Verify(
            s => s.ReserveReceiptAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Order_With_An_Existing_Receipt_Is_Acked_Without_Re_Reserving()
    {
        var order = BuildOrder(PaymentType.Cash, PaymentStatus.Pending);
        AttachReceipt(order, BuildReceipt());
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var ex = await Record.ExceptionAsync(() => CreateHandler().HandleAsync(Body(), CancellationToken.None));

        Assert.Null(ex);
        _receiptService.Verify(
            s => s.ReserveReceiptAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BlockingOnline_Country_With_No_Fiscal_Signature_Holds_The_Email()
    {
        var order = BuildOrder(PaymentType.Cash, PaymentStatus.Pending, CountryId);
        var receipt = BuildReceipt(); // no SetFiscalData → FiscalCode stays null

        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _receiptService
            .Setup(s => s.ReserveReceiptAsync(order, LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        // Realize does NOT stamp a code (the authority hasn't signed yet) — FiscalCode remains null.
        _receiptService
            .Setup(s => s.RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => AttachReceipt(order, receipt))
            .Returns(Task.CompletedTask);

        var deConfig = CountryConfiguration.Create(CountryId, "eur", "de", 19m);
        deConfig.UpdateFiscalEnforcementMode(FiscalEnforcementMode.BlockingOnline);
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deConfig);

        var ex = await Record.ExceptionAsync(() => CreateHandler().HandleAsync(Body(), CancellationToken.None));

        // Held, not failed: the claim committed (so a redelivery is deduped) but the email is NOT sent
        // and the PDF is not downloaded — the retry job releases it once the signature arrives.
        Assert.Null(ex);
        _emailService.Verify(
            s => s.SendOrderReceiptEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<byte[]?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _receiptService.Verify(
            s => s.DownloadReceiptPdfAsync(It.IsAny<OrderReceipt>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
