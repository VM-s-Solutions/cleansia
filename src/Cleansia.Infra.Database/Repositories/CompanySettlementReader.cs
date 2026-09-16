using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

/// <summary>
/// Every count is a filtered read, so the facts are the ambient company's alone (ADR-0064 D3). The
/// wind-down cut-off is midnight of <c>WindDownFrom</c> in each market's zone, the instant the sweep
/// itself cancels from, so the page's count and the sweep's selection agree.
/// </summary>
public sealed class CompanySettlementReader(CleansiaDbContext context, ITenantProvider tenantProvider)
    : ICompanySettlementReader
{
    private static readonly OrderStatus[] OpenStatuses = [.. OrderAvailability.OfferableStatuses];

    private static readonly DisputeStatus[] OpenDisputeStatuses =
        [DisputeStatus.Pending, DisputeStatus.UnderReview, DisputeStatus.WaitingForResponse, DisputeStatus.Escalated];

    private static readonly PaymentStatus[] CardMoneyMoved =
        [PaymentStatus.Paid, PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded];

    public async Task<CompanySettlementFacts> ReadAsync(CancellationToken cancellationToken)
    {
        var receipts = context.OrderReceipts;

        var openOrders = await context.Orders
            .CountAsync(o => OpenStatuses.Contains(o.CurrentStatus), cancellationToken);
        var openOrdersOnOrAfterWindDownFrom = await CountOpenOrdersOnOrAfterWindDownFromAsync(cancellationToken);
        var activeTemplates = await context.RecurringBookingTemplates
            .CountAsync(t => t.IsActive, cancellationToken);
        var activeMemberships = await context.UserMemberships
            .CountAsync(m => m.Status == MembershipStatus.Active, cancellationToken);
        var creditBalances = await context.CreditAccounts
            .CountAsync(a => a.Balance > 0, cancellationToken);
        var pendingRefunds = await context.Refunds
            .CountAsync(r => r.Status == RefundStatus.Pending, cancellationToken);
        var ordersAwaitingPay = await context.Orders
            .CountAsync(o => o.CurrentStatus == OrderStatus.Completed && !o.EmployeePayCalculated, cancellationToken);
        var ordersAwaitingReceipt = await context.Orders
            .CountAsync(
                o => (o.PaymentType == PaymentType.Cash || o.PaymentStatus == PaymentStatus.Paid)
                    && !receipts.Any(r => r.OrderId == o.Id),
                cancellationToken);
        var receiptsAwaitingFiscalRegistration = await receipts
            .CountAsync(r => r.FiscalNextRetryAt != null, cancellationToken);
        var openPayPeriods = await context.PayPeriods
            .CountAsync(p => p.Status == PayPeriodStatus.Open, cancellationToken);
        var closedPeriods = context.PayPeriods.Where(p => p.Status == PayPeriodStatus.Closed);
        var unpaidInvoices = await context.EmployeeInvoices
            .CountAsync(
                i => i.Status != EmployeeInvoiceStatus.Paid
                    && i.Status != EmployeeInvoiceStatus.Cancelled
                    && closedPeriods.Any(p => p.Id == i.PayPeriodId),
                cancellationToken);
        var uninvoicedPayRows = await context.OrderEmployeePays
            .CountAsync(
                pay => pay.EmployeeInvoiceId == null && closedPeriods.Any(p => p.Id == pay.PayPeriodId),
                cancellationToken);
        var openDisputes = await context.Disputes
            .CountAsync(d => OpenDisputeStatuses.Contains(d.Status), cancellationToken);
        var latestCardPaidCleaning = await context.Orders
            .Where(o => o.PaymentType == PaymentType.Card && CardMoneyMoved.Contains(o.PaymentStatus))
            .MaxAsync(o => (DateTime?)o.CleaningDateTime, cancellationToken);

        return new CompanySettlementFacts(
            OpenOrders: openOrders,
            OpenOrdersOnOrAfterWindDownFrom: openOrdersOnOrAfterWindDownFrom,
            ActiveTemplates: activeTemplates,
            ActiveMemberships: activeMemberships,
            CreditBalances: creditBalances,
            PendingRefunds: pendingRefunds,
            OrdersAwaitingPay: ordersAwaitingPay,
            OrdersAwaitingReceipt: ordersAwaitingReceipt,
            ReceiptsAwaitingFiscalRegistration: receiptsAwaitingFiscalRegistration,
            OpenPayPeriods: openPayPeriods,
            UnpaidInvoices: unpaidInvoices,
            UninvoicedPayRows: uninvoicedPayRows,
            OpenDisputes: openDisputes,
            LatestCardPaidCleaningDateTime: latestCardPaidCleaning);
    }

    private async Task<int> CountOpenOrdersOnOrAfterWindDownFromAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var windDownFrom = await context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.WindDownFrom)
            .FirstOrDefaultAsync(cancellationToken);
        if (windDownFrom is not { } from)
        {
            return 0;
        }

        var markets = await context.CountryConfigurations
            .Select(c => new { c.CountryId, c.TimeZoneId })
            .ToListAsync(cancellationToken);
        var utcCutoff = CutoffUtc(from, timeZoneId: null);

        var count = 0;
        foreach (var market in markets)
        {
            var cutoff = CutoffUtc(from, market.TimeZoneId);
            count += await context.Orders.CountAsync(
                o => OpenStatuses.Contains(o.CurrentStatus)
                    && o.CustomerAddress != null
                    && o.CustomerAddress.CountryId == market.CountryId
                    && o.CleaningDateTime >= cutoff,
                cancellationToken);
        }

        var marketCountryIds = markets.Select(m => m.CountryId).ToList();
        count += await context.Orders.CountAsync(
            o => OpenStatuses.Contains(o.CurrentStatus)
                && (o.CustomerAddress == null || !marketCountryIds.Contains(o.CustomerAddress.CountryId))
                && o.CleaningDateTime >= utcCutoff,
            cancellationToken);

        return count;
    }

    private static DateTime CutoffUtc(DateOnly windDownFrom, string? timeZoneId)
    {
        var midnight = windDownFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(midnight, ResolveZone(timeZoneId));
    }

    // FindSystemTimeZoneById throws on an unknown id and a settlement read must never throw over one;
    // UTC is the sweep's own fallback for a market without a zone.
    private static TimeZoneInfo ResolveZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
