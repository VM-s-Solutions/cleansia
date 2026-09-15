using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Services;

public class IncidentFileService(
    IUserRepository userRepository,
    IOrderRepository orderRepository,
    IRefundRepository refundRepository,
    IDisputeRepository disputeRepository,
    IUserConsentRepository userConsentRepository,
    ICurrencyRepository currencyRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    ICustomerActionAuditRepository customerActionAuditRepository,
    IAdminActionAuditRepository adminActionAuditRepository,
    IEmployeeActionAuditRepository employeeActionAuditRepository) : IIncidentFileService
{
    /// <summary>
    /// Newest rows of each source that are printed. A trail past this is cut and the file says so; the
    /// render holds a process-wide lock and a runaway document would hold it for everyone.
    /// </summary>
    public const int MaxTrailRowsPerSource = 2000;

    public const string CustomerSource = "Customer";
    public const string AdminSource = "Admin";
    public const string CleanerSource = "Cleaner";

    public async Task<bool> IsSubjectOrderAsync(string userId, string orderId, CancellationToken cancellationToken)
    {
        var proven = await ProvenOrderIdsAsync(userId, cancellationToken);
        return await orderRepository.GetQueryable()
            .AnyAsync(o => o.Id == orderId && (o.UserId == userId || proven.Contains(o.Id)), cancellationToken);
    }

    public async Task<IncidentFilePdfData> BuildAsync(string userId, string? orderId, string generatedBy, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetQueryable()
            .AsNoTracking()
            .FirstAsync(u => u.Id == userId, cancellationToken);

        var currencyCodes = await currencyRepository.GetQueryable()
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);

        var orders = await LoadOrdersAsync(userId, orderId, cancellationToken);
        var orderIds = orders.Select(o => o.Id).ToList();
        var orderNumbers = orders.ToDictionary(o => o.Id, o => o.DisplayOrderNumber);

        var refunds = await refundRepository.GetQueryable()
            .Where(r => orderIds.Contains(r.OrderId))
            .OrderBy(r => r.CreatedOn)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var disputes = await disputeRepository.GetQueryable()
            .Where(d => orderIds.Contains(d.OrderId))
            .Include(d => d.Messages)
            .Include(d => d.Evidence)
            .OrderBy(d => d.CreatedOn)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var disputeIds = disputes.Select(d => d.Id).ToList();

        var consents = await userConsentRepository.GetQueryable()
            .Where(c => c.UserId == userId)
            .Include(c => c.LegalDocument)
            .OrderBy(c => c.ConsentType)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var (trail, truncated) = await LoadTrailAsync(userId, orderId, orderIds, disputeIds, currencyCodes, cancellationToken);
        var (operatorName, market) = await ResolveOperatorAsync(user.TenantId, cancellationToken);

        var erased = !user.IsActive && GdprAuditReasons.IsErasure(user.DeactivatedBy);

        return new IncidentFilePdfData(
            new IncidentFileSubject(
                user.Id, user.FirstName, user.LastName, user.Email, user.PhoneNumber, user.CreatedOn,
                operatorName, market, user.PreferredLanguageCode, erased, erased ? user.DeactivatedOn : null),
            orderId,
            orders.Select(o => MapOrder(o, refunds.Where(r => r.OrderId == o.Id).ToList())).ToList(),
            disputes.Select(d => MapDispute(d, orderNumbers[d.OrderId])).ToList(),
            consents.Select(MapConsent).ToList(),
            trail,
            truncated,
            DateTimeOffset.UtcNow,
            generatedBy);
    }

    private async Task<List<Order>> LoadOrdersAsync(string userId, string? orderId, CancellationToken cancellationToken)
    {
        var proven = await ProvenOrderIdsAsync(userId, cancellationToken);
        var query = orderRepository.GetQueryable()
            .Where(o => o.UserId == userId || proven.Contains(o.Id));
        if (orderId is not null)
        {
            query = query.Where(o => o.Id == orderId);
        }

        return await query
            .Include(o => o.CustomerAddress)
            .Include(o => o.Currency)
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.SelectedServices).ThenInclude(s => s.Service)
            .Include(o => o.SelectedPackages).ThenInclude(p => p.Package)
            .Include(o => o.SelectedExtras)
            .Include(o => o.AssignedEmployees).ThenInclude(e => e.Employee).ThenInclude(e => e!.User)
            .AsSplitQuery()
            .AsNoTracking()
            .OrderBy(o => o.CreatedOn)
            .ToListAsync(cancellationToken);
    }

    // The market registry is the only edge into Tenants: an operator is named by the countries it
    // serves, so the display name and the markets come from one read of the configurations that name it.
    private async Task<(string? OperatorName, string? Market)> ResolveOperatorAsync(string? tenantId, CancellationToken cancellationToken)
    {
        if (tenantId is null)
        {
            return (null, null);
        }

        var markets = await countryConfigurationRepository.GetQueryable()
            .Where(c => c.OperatorTenantId == tenantId)
            .Include(c => c.Country)
            .Include(c => c.OperatorTenant)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (markets.Count == 0)
        {
            return (null, null);
        }

        var countries = markets
            .Where(c => c.Country is not null)
            .Select(c => $"{c.Country!.Name} ({c.Country.IsoAlpha2})")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return (markets[0].OperatorTenant?.Name, countries.Count == 0 ? null : string.Join(", ", countries));
    }

    private Task<List<string>> ProvenOrderIdsAsync(string userId, CancellationToken cancellationToken) =>
        GetActionTimeline.ProvenOrderIds(customerActionAuditRepository.GetQueryable(), userId)
            .ToListAsync(cancellationToken);

    private async Task<(IReadOnlyList<IncidentFileTrailEntry> Trail, bool Truncated)> LoadTrailAsync(
        string userId,
        string? orderId,
        List<string> orderIds,
        List<string> disputeIds,
        Dictionary<string, string> currencyCodes,
        CancellationToken cancellationToken)
    {
        // Scoped to an order, the customer arm is the subject's rows that name the order or its disputes
        // plus the guest rows on it. The orders this file walks are the account's and the proven ones —
        // NOT the e-mail-matched guest bookings the erasure and the export reach through SubjectOrders,
        // so a guest booking placed before the account existed is in this file only when an admin names
        // it by id. A stranger's refused probe at the order is left out: its user id and request context
        // are theirs, not the subject's, and this document leaves the platform. Unscoped, it is the
        // subject's own rows, like the export.
        var customer = orderId is null
            ? customerActionAuditRepository.GetQueryable().Where(a => a.UserId == userId)
            : customerActionAuditRepository.GetQueryable().Where(a => (a.UserId == userId || a.UserId == null) && a.ResourceId != null && (
                (a.ResourceType == nameof(Order) && a.ResourceId == orderId)
                || (a.ResourceType == nameof(Dispute) && disputeIds.Contains(a.ResourceId))));

        var admin = adminActionAuditRepository.GetQueryable().Where(a => a.ResourceId != null && (
            (orderId == null && a.ResourceType == nameof(User) && a.ResourceId == userId)
            || (a.ResourceType == nameof(Order) && orderIds.Contains(a.ResourceId))
            || (a.ResourceType == nameof(Dispute) && disputeIds.Contains(a.ResourceId))));

        var employee = employeeActionAuditRepository.GetQueryable().Where(a => orderIds.Contains(a.OrderId));

        const int window = MaxTrailRowsPerSource + 1;
        var customerRows = await customer.OrderByDescending(a => a.OccurredOn).ThenBy(a => a.Id).Take(window).AsNoTracking().ToListAsync(cancellationToken);
        var adminRows = await admin.OrderByDescending(a => a.OccurredOn).ThenBy(a => a.Id).Take(window).AsNoTracking().ToListAsync(cancellationToken);
        var employeeRows = await employee.OrderByDescending(a => a.CreatedOn).ThenBy(a => a.Id).Take(window).AsNoTracking().ToListAsync(cancellationToken);

        var truncated = customerRows.Count > MaxTrailRowsPerSource
                        || adminRows.Count > MaxTrailRowsPerSource
                        || employeeRows.Count > MaxTrailRowsPerSource;

        var trail = customerRows.Take(MaxTrailRowsPerSource).Select(a => MapCustomerRow(a, currencyCodes))
            .Concat(adminRows.Take(MaxTrailRowsPerSource).Select(a => MapAdminRow(a, currencyCodes)))
            .Concat(employeeRows.Take(MaxTrailRowsPerSource).Select(MapEmployeeRow))
            .ToList();

        return (trail, truncated);
    }

    private static IncidentFileTrailEntry MapCustomerRow(CustomerActionAudit row, Dictionary<string, string> currencyCodes) =>
        new(
            CustomerSource,
            row.OccurredOn,
            row.UserId is null ? "Guest" : nameof(UserProfile.Customer),
            row.UserId,
            row.Action,
            row.ResourceType,
            row.ResourceId,
            row.Success,
            row.ErrorCode,
            IncidentFileEvidenceFields.Flatten(row.PayloadJson, currencyCodes),
            row.IpAddress,
            row.DeviceLabel);

    private static IncidentFileTrailEntry MapAdminRow(AdminActionAudit row, Dictionary<string, string> currencyCodes)
    {
        var evidence = new List<IncidentFileEvidenceField>();
        if (row.Reason is not null)
        {
            evidence.Add(new IncidentFileEvidenceField("reason", row.Reason));
        }

        evidence.AddRange(IncidentFileEvidenceFields.Flatten(row.BeforeJson, currencyCodes, "before"));
        evidence.AddRange(IncidentFileEvidenceFields.Flatten(row.AfterJson, currencyCodes, "after"));

        return new IncidentFileTrailEntry(
            AdminSource,
            row.OccurredOn,
            row.ActorProfile.ToString(),
            row.ActorId,
            row.Action,
            row.ResourceType,
            row.ResourceId,
            row.Success,
            row.ErrorCode,
            evidence,
            IpAddress: null,
            DeviceLabel: null);
    }

    private static IncidentFileTrailEntry MapEmployeeRow(EmployeeActionAudit row) =>
        new(
            CleanerSource,
            row.CreatedOn,
            nameof(UserProfile.Employee),
            row.EmployeeId,
            GetActionTimeline.EmployeeActionLabel(row.Action),
            nameof(Order),
            row.OrderId,
            Success: true,
            ErrorCode: null,
            Evidence: [],
            IpAddress: null,
            DeviceLabel: null);

    private static IncidentFileOrder MapOrder(Order order, List<Refund> refunds)
    {
        var currency = order.Currency?.Code ?? order.CurrencyId;
        var address = order.CustomerAddress is { } a
            ? string.Join(", ", new[] { a.Street, a.ZipCode, a.City, a.State, a.CountryId }.Where(part => !string.IsNullOrWhiteSpace(part)))
            : "—";

        var lines = order.SelectedServices
            .Select(s => new IncidentFileOrderLine("Service", s.Service?.Name ?? s.ServiceId, s.LineTotal))
            .Concat(order.SelectedPackages.Select(p => new IncidentFileOrderLine("Package", p.Package?.Name ?? p.PackageId, p.LineTotal)))
            .Concat(order.SelectedExtras.Select(e => new IncidentFileOrderLine("Extra", e.Slug, e.UnitPrice)))
            .ToList();

        return new IncidentFileOrder(
            order.Id,
            order.DisplayOrderNumber,
            order.CreatedOn,
            order.CleaningDateTime,
            order.CompletedAt,
            order.CancelledAt,
            address,
            lines,
            order.TotalPrice,
            currency,
            order.PaymentType.ToString(),
            order.PaymentStatus.ToString(),
            order.CurrentStatus.ToString(),
            order.OrderStatusHistory
                .OrderBy(t => t.Sequence)
                .Select(t => new IncidentFileStatusChange(t.Status.ToString(), t.CreatedOn))
                .ToList(),
            refunds.Select(r => new IncidentFileRefund(
                r.Amount, r.Currency, r.Reason.ToString(), r.Source.ToString(), r.Status.ToString(), r.CreatedOn, r.ConfirmedOn)).ToList(),
            order.AssignedEmployees
                .OrderBy(e => e.SeatOrdinal)
                .Select(e => new IncidentFileCleaner(e.EmployeeId, e.Employee?.User?.FirstName ?? "—"))
                .ToList(),
            order.CancelledBy?.ToString(),
            order.CancellationReason);
    }

    private static IncidentFileDispute MapDispute(Dispute dispute, string orderNumber) =>
        new(
            dispute.Id,
            orderNumber,
            dispute.Reason.ToString(),
            dispute.Status.ToString(),
            dispute.Description,
            dispute.CreatedOn,
            dispute.Messages
                .OrderBy(m => m.CreatedOn)
                .Select(m => new IncidentFileDisputeMessage(m.IsStaffMessage ? "Staff" : nameof(UserProfile.Customer), m.AuthorId, m.CreatedOn, m.Message))
                .ToList(),
            dispute.Evidence.Select(e => e.FileName).ToList(),
            dispute.ResolutionNotes,
            dispute.RefundAmount,
            dispute.ResolvedBy,
            dispute.ResolvedOn);

    private static IncidentFileConsent MapConsent(UserConsent consent) =>
        new(
            consent.ConsentType.ToString(),
            consent.DocumentVersion,
            consent.LegalDocument?.EffectiveFrom,
            consent.IsGranted,
            consent.GrantedAt,
            consent.WithdrawnAt,
            consent.IpAddress,
            consent.UserAgent);
}
