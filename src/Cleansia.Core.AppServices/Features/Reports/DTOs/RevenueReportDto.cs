namespace Cleansia.Core.AppServices.Features.Reports.DTOs;

public record RevenueReportDto(
    decimal TotalRevenue,
    decimal AverageOrderValue,
    int TotalOrders,
    int CompletedOrders,
    int CancelledOrders,
    decimal GrowthPercentage,
    IEnumerable<DailyRevenue> DailyRevenues,
    IEnumerable<RevenueByService> RevenueByService,
    IEnumerable<RevenueByPackage> RevenueByPackage,
    IEnumerable<RevenueByPaymentType> RevenueByPaymentType,
    IEnumerable<RevenueByPaymentStatus> RevenueByPaymentStatus);

public record DailyRevenue(
    DateOnly Date,
    decimal Amount,
    int OrderCount);

public record RevenueByService(
    string ServiceId,
    string ServiceName,
    decimal TotalRevenue,
    int OrderCount);

public record RevenueByPackage(
    string PackageId,
    string PackageName,
    decimal TotalRevenue,
    int OrderCount);

public record RevenueByPaymentType(
    string PaymentTypeCode,
    string PaymentTypeName,
    decimal TotalRevenue,
    int OrderCount);

public record RevenueByPaymentStatus(
    string PaymentStatusCode,
    string PaymentStatusName,
    decimal TotalRevenue,
    int OrderCount);