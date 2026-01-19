#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Reports.DTOs;
using Cleansia.Core.AppServices.Features.Reports.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Reports;

public class GetRevenueReport
{
    public record Query(ReportFilter Filter) : IQuery<RevenueReportDto>;

    internal class Handler(IOrderRepository orderRepository)
        : IRequestHandler<Query, BusinessResult<RevenueReportDto>>
    {
        public async Task<BusinessResult<RevenueReportDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var orders = await orderRepository
                .GetOrdersByDateRange(request.Filter.StartDate, request.Filter.EndDate)
                .ToListAsync(cancellationToken);

            var totalRevenue = orders.Sum(o => o.TotalPrice);
            var totalOrders = orders.Count;
            var completedOrders = orders.Count(o => o.GetCurrentOrderStatus() == OrderStatus.Completed);
            var cancelledOrders = orders.Count(o => o.GetCurrentOrderStatus() == OrderStatus.Cancelled);
            var averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

            var dailyRevenues = orders
                .GroupBy(o => DateOnly.FromDateTime(o.CleaningDateTime))
                .Select(g => new DailyRevenue(
                    Date: g.Key,
                    Amount: g.Sum(o => o.TotalPrice),
                    OrderCount: g.Count()))
                .OrderBy(d => d.Date)
                .ToList();

            var revenueByService = orders
                .SelectMany(o => o.SelectedServices.Select(s => new { Order = o, Service = s.Service }))
                .GroupBy(x => new { x.Service.Id, x.Service.Name })
                .Select(g => new RevenueByService(
                    ServiceId: g.Key.Id,
                    ServiceName: g.Key.Name,
                    TotalRevenue: g.Sum(x => x.Order.TotalPrice / (x.Order.SelectedServices.Count > 0 ? x.Order.SelectedServices.Count : 1)),
                    OrderCount: g.Select(x => x.Order.Id).Distinct().Count()))
                .ToList();

            var revenueByPackage = orders
                .SelectMany(o => o.SelectedPackages.Select(p => new { Order = o, Package = p.Package }))
                .GroupBy(x => new { x.Package.Id, x.Package.Name })
                .Select(g => new RevenueByPackage(
                    PackageId: g.Key.Id,
                    PackageName: g.Key.Name,
                    TotalRevenue: g.Sum(x => x.Order.TotalPrice / (x.Order.SelectedPackages.Count > 0 ? x.Order.SelectedPackages.Count : 1)),
                    OrderCount: g.Select(x => x.Order.Id).Distinct().Count()))
                .ToList();

            var revenueByPaymentType = orders
                .GroupBy(o => o.PaymentType)
                .Select(g => new RevenueByPaymentType(
                    PaymentTypeCode: g.Key.ToString(),
                    PaymentTypeName: g.Key.MapToCode().Name,
                    TotalRevenue: g.Sum(o => o.TotalPrice),
                    OrderCount: g.Count()))
                .ToList();

            var revenueByPaymentStatus = orders
                .GroupBy(o => o.PaymentStatus)
                .Select(g => new RevenueByPaymentStatus(
                    PaymentStatusCode: g.Key.ToString(),
                    PaymentStatusName: g.Key.MapToCode().Name,
                    TotalRevenue: g.Sum(o => o.TotalPrice),
                    OrderCount: g.Count()))
                .ToList();

            var growthPercentage = CalculateGrowthPercentage(dailyRevenues);

            return new RevenueReportDto(
                TotalRevenue: totalRevenue,
                AverageOrderValue: averageOrderValue,
                TotalOrders: totalOrders,
                CompletedOrders: completedOrders,
                CancelledOrders: cancelledOrders,
                GrowthPercentage: growthPercentage,
                DailyRevenues: dailyRevenues,
                RevenueByService: revenueByService,
                RevenueByPackage: revenueByPackage,
                RevenueByPaymentType: revenueByPaymentType,
                RevenueByPaymentStatus: revenueByPaymentStatus);
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