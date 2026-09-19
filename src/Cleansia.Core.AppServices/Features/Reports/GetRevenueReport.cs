#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Reports.DTOs;
using Cleansia.Core.AppServices.Features.Reports.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MediatR;

namespace Cleansia.Core.AppServices.Features.Reports;

/// <summary>
/// Revenue is completed and paid orders by completion date, in one currency, minus every refund on
/// those orders — card refunds and credit returned — whatever the refund's date. A refund therefore
/// reduces the month the order completed in, not the month it was issued.
/// </summary>
public class GetRevenueReport
{
    public record Query(ReportFilter Filter) : IQuery<RevenueReportDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(ICurrencyRepository currencyRepository)
        {
            // Optional: null is the platform default. A NAMED currency has to exist, or a typo would
            // answer an all-zero report that reads as "no revenue".
            RuleFor(x => x.Filter.CurrencyId)
                .MustAsync(async (id, ct) => await currencyRepository.ExistsAsync(id!, ct))
                .WithMessage(BusinessErrorMessage.CurrencyNotFound)
                .When(x => !string.IsNullOrWhiteSpace(x.Filter.CurrencyId));
        }
    }

    internal class Handler(
        IOrderRepository orderRepository,
        IRefundRepository refundRepository,
        ICreditAccountRepository creditAccountRepository,
        ICurrencyRepository currencyRepository)
        : IRequestHandler<Query, BusinessResult<RevenueReportDto>>
    {
        public async Task<BusinessResult<RevenueReportDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            // ONE currency per report. Every figure below is a sum, and a sum across currencies is
            // not a number — so the admin picks one (or takes the default) and the query filters on it.
            var currency = string.IsNullOrWhiteSpace(request.Filter.CurrencyId)
                ? await currencyRepository.GetDefaultAsync(cancellationToken)
                : await currencyRepository.GetByIdAsync(request.Filter.CurrencyId, cancellationToken);
            if (currency is null)
            {
                return BusinessResult.Failure<RevenueReportDto>(new Error(
                    nameof(request.Filter.CurrencyId), BusinessErrorMessage.CurrencyNotFound));
            }

            var orders = await orderRepository.GetCompletedPaidOrdersByCompletionDateAsync(
                request.Filter.StartDate, request.Filter.EndDate, currency.Id, cancellationToken);
            var orderIds = orders.Select(o => o.Id).ToList();

            // A refund has two legs: the card share is a Refund row, the credit share went back to the
            // customer's balance. Both are subtracted from the order they belong to, whatever their date.
            var cardRefunds = await refundRepository.GetSucceededRefundTotalsByOrderAsync(orderIds, cancellationToken);
            var creditReturns = await creditAccountRepository.GetReturnedTotalsByOrderAsync(orderIds, cancellationToken);

            decimal RefundedToCardOf(Order o) => cardRefunds.GetValueOrDefault(o.Id, 0m);
            decimal ReturnedToCreditOf(Order o) => creditReturns.GetValueOrDefault(o.Id, 0m);
            decimal RefundedOf(Order o) => RefundedToCardOf(o) + ReturnedToCreditOf(o);
            decimal NetOf(Order o) => o.TotalPrice - RefundedOf(o);

            var totalRevenue = orders.Sum(o => o.TotalPrice);
            var totalOrders = orders.Count;
            var averageOrderValue = totalOrders > 0 ? orders.Sum(NetOf) / totalOrders : 0;

            var dailyRevenues = orders
                .GroupBy(o => DateOnly.FromDateTime(o.CompletedAt!.Value))
                .Select(g => new DailyRevenue(
                    Date: g.Key,
                    Amount: g.Sum(NetOf),
                    OrderCount: g.Count(),
                    Refunded: g.Sum(RefundedOf)))
                .OrderBy(d => d.Date)
                .ToList();

            var revenueByService = orders
                .SelectMany(o => o.SelectedServices.Select(s => new { Order = o, Service = s.Service }))
                .GroupBy(x => new { x.Service.Id, x.Service.Name })
                .Select(g => new RevenueByService(
                    ServiceId: g.Key.Id,
                    ServiceName: g.Key.Name,
                    TotalRevenue: g.Sum(x => NetOf(x.Order) / Math.Max(1, x.Order.SelectedServices.Count)),
                    OrderCount: g.Select(x => x.Order.Id).Distinct().Count()))
                .ToList();

            var revenueByPackage = orders
                .SelectMany(o => o.SelectedPackages.Select(p => new { Order = o, Package = p.Package }))
                .GroupBy(x => new { x.Package.Id, x.Package.Name })
                .Select(g => new RevenueByPackage(
                    PackageId: g.Key.Id,
                    PackageName: g.Key.Name,
                    TotalRevenue: g.Sum(x => NetOf(x.Order) / Math.Max(1, x.Order.SelectedPackages.Count)),
                    OrderCount: g.Select(x => x.Order.Id).Distinct().Count()))
                .ToList();

            // Grouped by the tender actually taken, so a card booking the cleaner settled in cash lands
            // in the cash column the admin has to reconcile against the float. Gross here: the row
            // reconciles against a gateway statement, which lists charges and refunds separately.
            var revenueByPaymentType = orders
                .GroupBy(o => o.ActualPaymentType)
                .Select(g => new RevenueByPaymentType(
                    PaymentTypeCode: g.Key.ToString(),
                    PaymentTypeName: g.Key.MapToCode().Name,
                    TotalRevenue: g.Sum(o => o.TotalPrice),
                    OrderCount: g.Count(),
                    SettledFromCredit: g.Sum(o => o.CreditAppliedAmount),
                    RefundedToCard: g.Sum(RefundedToCardOf),
                    ReturnedToCredit: g.Sum(ReturnedToCreditOf)))
                .ToList();

            var revenueByPaymentStatus = orders
                .GroupBy(o => o.PaymentStatus)
                .Select(g => new RevenueByPaymentStatus(
                    PaymentStatusCode: g.Key.ToString(),
                    PaymentStatusName: g.Key.MapToCode().Name,
                    TotalRevenue: g.Sum(o => o.TotalPrice),
                    OrderCount: g.Count()))
                .ToList();

            // Cancelled bookings are not revenue; they are counted on their own axis so the card stays
            // on the page.
            var cancelledOrders = await orderRepository.CountCancelledBookingsInPeriodAsync(
                request.Filter.StartDate, request.Filter.EndDate, currency.Id, cancellationToken);

            var growthPercentage = CalculateGrowthPercentage(dailyRevenues);

            return new RevenueReportDto(
                TotalRevenue: totalRevenue,
                AverageOrderValue: averageOrderValue,
                TotalOrders: totalOrders,
                CompletedOrders: totalOrders,
                CancelledOrders: cancelledOrders,
                GrowthPercentage: growthPercentage,
                DailyRevenues: dailyRevenues,
                RevenueByService: revenueByService,
                RevenueByPackage: revenueByPackage,
                RevenueByPaymentType: revenueByPaymentType,
                RevenueByPaymentStatus: revenueByPaymentStatus,
                TotalSettledFromCredit: orders.Sum(o => o.CreditAppliedAmount),
                CurrencyCode: currency.Code,
                TotalRefundedToCard: orders.Sum(RefundedToCardOf),
                TotalReturnedToCredit: orders.Sum(ReturnedToCreditOf));
        }

        private static decimal CalculateGrowthPercentage(List<DailyRevenue> dailyRevenues)
        {
            if (dailyRevenues.Count < 2)
                return 0;

            var midPoint = dailyRevenues.Count / 2;
            var firstHalf = dailyRevenues.Take(midPoint).Sum(d => d.Amount);
            var secondHalf = dailyRevenues.Skip(midPoint).Sum(d => d.Amount);

            return firstHalf > 0
                ? ((secondHalf - firstHalf) / firstHalf) * 100
                : 0;
        }
    }
}
