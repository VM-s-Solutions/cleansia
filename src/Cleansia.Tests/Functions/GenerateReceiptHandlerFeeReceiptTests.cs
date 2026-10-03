using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// A paid receivable earns a fee receipt of its own next to the order's sale receipt: reserved on its own
/// number, committed, then realized — on an order whose sale was never paid, such as a cash booking
/// cancelled late. A receivable not yet paid, or one whose fee receipt the order already holds, gets none,
/// and the sale receipt is never asked for.
/// </summary>
public class GenerateReceiptHandlerFeeReceiptTests
{
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4F5";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IReceiptService> _receiptService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Order _order;
    private readonly Receivable _receivable;

    public GenerateReceiptHandlerFeeReceiptTests()
    {
        _unitOfWork
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDbContextTransaction>());

        _order = Order.Create(
            customerName: "Owing Customer",
            customerEmail: "owing@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-owing",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        _order.Id = OrderId;
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_order);

        _receivable = Receivable.ForCashCancellationFee(_order, 375m);
        _receivables
            .Setup(r => r.GetByIdAsync(_receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_receivable);

        _receiptService
            .Setup(s => s.ReserveFeeReceiptAsync(_order, _receivable, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => OrderReceipt.CreateFee(_receivable, "RCP-2026-0042", "fee.pdf", "2026/ORD/fee.pdf", "language-en"));
    }

    private GenerateReceiptHandler CreateHandler() => new(
        _orderRepository.Object,
        _receiptService.Object,
        _emailService.Object,
        TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
        Mock.Of<ICountryConfigurationRepository>(),
        _unitOfWork.Object,
        Mock.Of<ITenantProvider>(),
        new ArchivedCompanyDeadLetter(Mock.Of<IServiceScopeFactory>(), NullLogger<ArchivedCompanyDeadLetter>.Instance),
        _receivables.Object,
        NullLogger<GenerateReceiptHandler>.Instance);

    private string Body() => JsonSerializer.Serialize(
        new QueueEnvelope<GenerateReceiptMessage>(
            MessageKeys.FeeReceipt(_receivable.Id), null, new GenerateReceiptMessage(OrderId, "en", _receivable.Id)),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task A_Paid_Receivable_On_An_Unpaid_Order_Gets_Its_Fee_Receipt_Reserved_Committed_And_Realized()
    {
        _receivable.MarkPaid("pi_link", DateTimeOffset.UtcNow);

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        _receiptService.Verify(s => s.ReserveFeeReceiptAsync(_order, _receivable, "en", It.IsAny<CancellationToken>()), Times.Once);
        _receiptService.Verify(s => s.RealizeFiscalAndPdfAsync(
            _order, It.Is<OrderReceipt>(r => r.ReceivableId == _receivable.Id), It.IsAny<CancellationToken>()), Times.Once);
        _receiptService.Verify(s => s.ReserveReceiptAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _emailService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Receivable_Not_Yet_Paid_Gets_No_Receipt()
    {
        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        _receiptService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Receivable_Whose_Fee_Receipt_The_Order_Holds_Is_Not_Issued_Twice()
    {
        _receivable.MarkPaid("pi_link", DateTimeOffset.UtcNow);
        OrderReceiptAttachment.Attach(_order, OrderReceipt.CreateFee(_receivable, "RCP-2026-0041", "fee.pdf", "blob", "language-en"));

        await CreateHandler().HandleAsync(Body(), CancellationToken.None);

        _receiptService.VerifyNoOtherCalls();
    }
}
