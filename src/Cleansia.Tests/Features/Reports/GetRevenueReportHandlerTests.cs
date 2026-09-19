using System.Reflection;
using Cleansia.Core.AppServices.Features.Reports;
using Cleansia.Core.AppServices.Features.Reports.DTOs;
using Cleansia.Core.AppServices.Features.Reports.Filters;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Moq;

namespace Cleansia.Tests.Features.Reports;

/// <summary>
/// The revenue report is "paid and completed orders, minus refunds, per currency, by completion date"
/// (owner ruling 2026-09-19). The repositories answer the SET — completed, paid, completed inside the
/// period, one currency — and the two refund legs per order; the handler's arithmetic is what these
/// tests pin: net per order over BOTH legs, whatever the refund's date, gross kept for the gateway
/// reconciliation, and every breakdown over net.
/// </summary>
public class GetRevenueReportHandlerTests
{
    private const string CzkId = "currency-czk";
    private const string EurId = "currency-eur";

    private static readonly DateTime Start = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime March5 = new(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime March20 = new(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Currency Czk = CurrencyWithId("CZK", CzkId);
    private static readonly Currency Eur = CurrencyWithId("EUR", EurId);

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IRefundRepository> _refundRepository = new();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

    public GetRevenueReportHandlerTests()
    {
        _currencyRepository.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Czk);
        _currencyRepository.Setup(r => r.GetByIdAsync(EurId, It.IsAny<CancellationToken>())).ReturnsAsync(Eur);
        _orderRepository
            .Setup(r => r.CountCancelledBookingsInPeriodAsync(Start, End, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        Arrange([]);
    }

    private static Currency CurrencyWithId(string code, string id)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        return currency;
    }

    // The handler is internal, like GetPayrollReport's; built the way CompanyVatLeverTests builds its own.
    private object CreateHandler() =>
        Activator.CreateInstance(
            typeof(GetRevenueReport).GetNestedType("Handler", BindingFlags.NonPublic)!,
            _orderRepository.Object,
            _refundRepository.Object,
            _creditAccountRepository.Object,
            _currencyRepository.Object)!;

    private async Task<RevenueReportDto> ReportAsync(string? currencyId = null)
    {
        var handler = CreateHandler();
        var result = await (Task<BusinessResult<RevenueReportDto>>)handler.GetType()
            .GetMethod("Handle")!
            .Invoke(handler, [new GetRevenueReport.Query(new ReportFilter(Start, End, currencyId)), CancellationToken.None])!;
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private void Arrange(
        IReadOnlyList<Order> orders,
        IReadOnlyDictionary<string, decimal>? cardRefunds = null,
        IReadOnlyDictionary<string, decimal>? creditReturns = null,
        string currencyId = CzkId)
    {
        _orderRepository
            .Setup(r => r.GetCompletedPaidOrdersByCompletionDateAsync(Start, End, currencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);
        var ids = orders.Select(o => o.Id).ToHashSet();
        _refundRepository
            .Setup(r => r.GetSucceededRefundTotalsByOrderAsync(
                It.Is<IReadOnlyCollection<string>>(c => c.ToHashSet().SetEquals(ids)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cardRefunds ?? new Dictionary<string, decimal>());
        _creditAccountRepository
            .Setup(r => r.GetReturnedTotalsByOrderAsync(
                It.Is<IReadOnlyCollection<string>>(c => c.ToHashSet().SetEquals(ids)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditReturns ?? new Dictionary<string, decimal>());
    }

    private static Order CompletedPaidOrder(
        string id,
        decimal totalPrice,
        DateTime completedAtUtc,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        decimal creditApplied = 0m,
        DateTime? cleaningDateTime = null,
        Currency? currency = null)
    {
        var order = OrderMockFactory.Generate(
            new OrderMockFactory.OrderPartial
            {
                Id = id,
                TotalPrice = totalPrice,
                PaymentStatus = paymentStatus,
                CurrentStatus = OrderStatus.Completed,
                CleaningDateTime = cleaningDateTime ?? completedAtUtc.AddHours(-3),
            },
            currency: currency ?? Czk);
        order.MarkCompletedAt(completedAtUtc);
        if (creditApplied > 0m)
        {
            order.ApplyCredit(creditApplied, "test");
        }

        return order;
    }

    private static Service ServiceWithId(string id)
    {
        var service = Service.Create("cat-1", $"Service {id}", "d");
        service.Id = id;
        return service;
    }

    private static Package PackageWithId(string id)
    {
        var package = Package.Create($"Package {id}", "d");
        package.Id = id;
        return package;
    }

    [Fact]
    public async Task Cancelled_Orders_Come_From_Their_Own_Count_Not_From_The_Completed_Set()
    {
        Arrange([
            CompletedPaidOrder("a", 1000m, March5),
            CompletedPaidOrder("b", 2000m, March20),
        ]);
        _orderRepository
            .Setup(r => r.CountCancelledBookingsInPeriodAsync(Start, End, CzkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var report = await ReportAsync();

        Assert.Equal(1, report.CancelledOrders);
        Assert.Equal(2, report.TotalOrders);
        Assert.Equal(2, report.CompletedOrders);
        Assert.Equal(3000m, report.TotalRevenue);
    }

    /// <summary>A 1 000 paid, B 2 000 paid with a 300 card refund issued in April.</summary>
    [Fact]
    public async Task Gross_Both_Refund_Legs_Net_And_Average_Are_The_Rulings_Numbers()
    {
        Arrange(
            [CompletedPaidOrder("a", 1000m, March5), CompletedPaidOrder("b", 2000m, March20, PaymentStatus.PartiallyRefunded)],
            cardRefunds: new Dictionary<string, decimal> { ["b"] = 300m });

        var report = await ReportAsync();

        Assert.Equal(3000m, report.TotalRevenue);
        Assert.Equal(300m, report.TotalRefundedToCard);
        Assert.Equal(0m, report.TotalReturnedToCredit);
        Assert.Equal(300m, report.TotalRefunded);
        Assert.Equal(2700m, report.NetRevenue);
        Assert.Equal(1350m, report.AverageOrderValue);
        Assert.Equal(2, report.TotalOrders);
        Assert.Equal(2, report.CompletedOrders);
        Assert.Equal("CZK", report.CurrencyCode);
    }

    /// <summary>
    /// A 2 000 order settled 500 credit + 1 500 card, refunded in full: the Refunds table holds the
    /// 1 500 card leg and the 500 went back as credit. Netting the card leg alone would leave 500 of
    /// revenue for an order the customer has entirely back.
    /// </summary>
    [Fact]
    public async Task A_Fully_Refunded_Mixed_Tender_Order_Contributes_Zero()
    {
        Arrange(
            [CompletedPaidOrder("mixed", 2000m, March5, PaymentStatus.Refunded, creditApplied: 500m)],
            cardRefunds: new Dictionary<string, decimal> { ["mixed"] = 1500m },
            creditReturns: new Dictionary<string, decimal> { ["mixed"] = 500m });

        var report = await ReportAsync();

        Assert.Equal(2000m, report.TotalRevenue);
        Assert.Equal(1500m, report.TotalRefundedToCard);
        Assert.Equal(500m, report.TotalReturnedToCredit);
        Assert.Equal(0m, report.NetRevenue);
        Assert.Equal(1, report.TotalOrders);
        Assert.Equal(0m, report.AverageOrderValue);
    }

    [Fact]
    public async Task A_Partially_Refunded_Mixed_Tender_Order_Contributes_What_The_Customer_Kept_Paying()
    {
        Arrange(
            [CompletedPaidOrder("mixed", 2000m, March5, PaymentStatus.PartiallyRefunded, creditApplied: 500m)],
            cardRefunds: new Dictionary<string, decimal> { ["mixed"] = 750m },
            creditReturns: new Dictionary<string, decimal> { ["mixed"] = 250m });

        var report = await ReportAsync();

        Assert.Equal(1000m, report.NetRevenue);
        Assert.Equal(1000m, report.AverageOrderValue);
    }

    [Fact]
    public async Task Daily_Buckets_Are_Keyed_By_Completion_Date_And_Hold_Net()
    {
        // Booked on 4 March, completed on 5 March: the bucket follows the completion, and the 200
        // refund on it is netted out of that day.
        var booked = new DateTime(2026, 3, 4, 8, 0, 0, DateTimeKind.Utc);
        Arrange(
            [
                CompletedPaidOrder("a", 1000m, March5, PaymentStatus.PartiallyRefunded, cleaningDateTime: booked),
                CompletedPaidOrder("b", 500m, March20),
            ],
            cardRefunds: new Dictionary<string, decimal> { ["a"] = 200m });

        var report = await ReportAsync();

        var days = report.DailyRevenues.ToList();
        Assert.Equal(2, days.Count);
        Assert.Equal(new DateOnly(2026, 3, 5), days[0].Date);
        Assert.Equal(800m, days[0].Amount);
        Assert.Equal(200m, days[0].Refunded);
        Assert.Equal(1, days[0].OrderCount);
        Assert.Equal(new DateOnly(2026, 3, 20), days[1].Date);
        Assert.Equal(500m, days[1].Amount);
        Assert.Equal(0m, days[1].Refunded);
        Assert.DoesNotContain(days, d => d.Date == DateOnly.FromDateTime(booked));
    }

    [Fact]
    public async Task A_Fully_Refunded_Order_Counts_Contributes_Zero_And_Sits_Under_Refunded()
    {
        Arrange(
            [CompletedPaidOrder("a", 1000m, March5), CompletedPaidOrder("gone", 800m, March20, PaymentStatus.Refunded)],
            cardRefunds: new Dictionary<string, decimal> { ["gone"] = 800m });

        var report = await ReportAsync();

        Assert.Equal(2, report.TotalOrders);
        Assert.Equal(1000m, report.NetRevenue);
        var refunded = Assert.Single(report.RevenueByPaymentStatus, s => s.PaymentStatusCode == nameof(PaymentStatus.Refunded));
        Assert.Equal(1, refunded.OrderCount);
        Assert.Equal(800m, refunded.TotalRevenue);
    }

    [Fact]
    public async Task By_Service_Allocation_Splits_Net_Evenly_Across_The_Lines()
    {
        var order = CompletedPaidOrder("a", 1000m, March5, PaymentStatus.PartiallyRefunded);
        order.AddSelectedServices([
            OrderService.Create(order, ServiceWithId("svc-1"), 500m, 0m, 500m),
            OrderService.Create(order, ServiceWithId("svc-2"), 500m, 0m, 500m),
        ]);
        Arrange([order], cardRefunds: new Dictionary<string, decimal> { ["a"] = 200m });

        var report = await ReportAsync();

        var lines = report.RevenueByService.OrderBy(s => s.ServiceId).ToList();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal(400m, l.TotalRevenue));
        Assert.All(lines, l => Assert.Equal(1, l.OrderCount));
    }

    [Fact]
    public async Task By_Package_Allocation_Splits_Net_Evenly_Across_The_Lines()
    {
        var order = CompletedPaidOrder("a", 1000m, March5, PaymentStatus.PartiallyRefunded);
        order.AddSelectedPackages([
            OrderPackage.Create(order, PackageWithId("pkg-1"), 500m),
            OrderPackage.Create(order, PackageWithId("pkg-2"), 500m),
        ]);
        Arrange([order], creditReturns: new Dictionary<string, decimal> { ["a"] = 200m });

        var report = await ReportAsync();

        var lines = report.RevenueByPackage.OrderBy(p => p.PackageId).ToList();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal(400m, l.TotalRevenue));
    }

    /// <summary>
    /// A card order of 1 000 with 100 credit and a 200 refund split 180 card + 20 credit. The Card row
    /// is the line that matches the gateway statement: it took 900 and gave back 180.
    /// </summary>
    [Fact]
    public async Task By_Tender_Carries_Both_Legs_And_Net_On_Tender_Subtracts_The_Card_Leg_Only()
    {
        Arrange(
            [CompletedPaidOrder("a", 1000m, March5, PaymentStatus.PartiallyRefunded, creditApplied: 100m)],
            cardRefunds: new Dictionary<string, decimal> { ["a"] = 180m },
            creditReturns: new Dictionary<string, decimal> { ["a"] = 20m });

        var report = await ReportAsync();

        var card = Assert.Single(report.RevenueByPaymentType);
        Assert.Equal(nameof(PaymentType.Card), card.PaymentTypeCode);
        Assert.Equal(1000m, card.TotalRevenue);
        Assert.Equal(100m, card.SettledFromCredit);
        Assert.Equal(900m, card.SettledOnTender);
        Assert.Equal(180m, card.RefundedToCard);
        Assert.Equal(20m, card.ReturnedToCredit);
        Assert.Equal(720m, card.NetOnTender);
        Assert.Equal(100m, report.TotalSettledFromCredit);
        Assert.Equal(800m, report.NetRevenue);
    }

    [Fact]
    public async Task Growth_Is_Computed_Over_Net_Daily_Amounts()
    {
        // Gross is flat (1 000 each day); net is 500 then 1 000, so growth on net is +100 %.
        Arrange(
            [
                CompletedPaidOrder("a", 1000m, March5, PaymentStatus.PartiallyRefunded),
                CompletedPaidOrder("b", 1000m, March20),
            ],
            cardRefunds: new Dictionary<string, decimal> { ["a"] = 500m });

        var report = await ReportAsync();

        Assert.Equal(100m, report.GrowthPercentage);
    }

    [Fact]
    public async Task A_Named_Currency_Scopes_The_Set_And_Is_Named_On_The_Report()
    {
        Arrange([CompletedPaidOrder("e", 45.10m, March5, currency: Eur)], currencyId: EurId);

        var report = await ReportAsync(EurId);

        Assert.Equal("EUR", report.CurrencyCode);
        Assert.Equal(45.10m, report.TotalRevenue);
        Assert.Equal(45.10m, report.NetRevenue);
        _orderRepository.Verify(
            r => r.GetCompletedPaidOrdersByCompletionDateAsync(Start, End, CzkId, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task An_Empty_Period_Is_All_Zeros()
    {
        var report = await ReportAsync();

        Assert.Equal(0m, report.TotalRevenue);
        Assert.Equal(0m, report.NetRevenue);
        Assert.Equal(0m, report.AverageOrderValue);
        Assert.Equal(0, report.TotalOrders);
        Assert.Equal(0m, report.GrowthPercentage);
        Assert.Empty(report.DailyRevenues);
    }
}
