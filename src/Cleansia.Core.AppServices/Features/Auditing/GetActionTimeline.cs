using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Auditing;

/// <summary>
/// One <c>OccurredOn DESC</c> page over the three audit tables (ADR-0062 D6), keyed by a user or by a
/// resource. By user: the customer's own rows, the admin rows on the user or on their orders, disputes
/// and memberships, and the cleaner rows on their orders — the orders being those that name the user
/// or that the user's own successful acts named (<see cref="ProvenOrderIds"/>). By resource: every row
/// of the three tables that names it — which is the only way a guest act (<c>UserId</c> null) is ever
/// reached.
/// </summary>
public class GetActionTimeline
{
    public const int MaxLimit = 100;

    /// <summary>
    /// The admin and employee arms match the user through the ids of what they own; the set is capped
    /// at the most recent rows so the <c>IN (...)</c> list cannot grow with the account.
    /// </summary>
    public const int RecentResourceCap = 1000;

    /// <summary>The order is fixed newest-first; <c>Sort</c> is not read.</summary>
    public class Request : DataRangeRequest, IRequest<PagedData<TimelineEntryDto>>
    {
        public string? UserId { get; init; }
        public string? ResourceType { get; init; }
        public string? ResourceId { get; init; }
    }

    public class Validator : AbstractValidator<Request>
    {
        public Validator(IUserRepository userRepository)
        {
            When(r => !string.IsNullOrWhiteSpace(r.UserId), () =>
                RuleFor(r => r.UserId!).MustAsync(userRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.NotExistingUserWithId));
            RuleFor(x => x)
                .Must(KeyedByExactlyOneOfUserOrResource)
                .WithMessage(BusinessErrorMessage.TimelineFilterRequired)
                .WithName(nameof(Request.UserId));

            RuleFor(x => x.Limit)
                .LessThanOrEqualTo(MaxLimit)
                .WithMessage(BusinessErrorMessage.PageSizeExceeded);
        }

        private static bool KeyedByExactlyOneOfUserOrResource(Request request)
        {
            var hasUser = !string.IsNullOrWhiteSpace(request.UserId);
            var hasType = !string.IsNullOrWhiteSpace(request.ResourceType);
            var hasId = !string.IsNullOrWhiteSpace(request.ResourceId);

            return (hasUser && !hasType && !hasId) || (!hasUser && hasType && hasId);
        }
    }

    internal class Handler(
        ICustomerActionAuditRepository customerActionAuditRepository,
        IAdminActionAuditRepository adminActionAuditRepository,
        IEmployeeActionAuditRepository employeeActionAuditRepository,
        IOrderRepository orderRepository,
        IDisputeRepository disputeRepository,
        IUserMembershipRepository userMembershipRepository)
        : IRequestHandler<Request, PagedData<TimelineEntryDto>>
    {
        public async Task<PagedData<TimelineEntryDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var (customer, admin, employee) = string.IsNullOrWhiteSpace(request.UserId)
                ? ByResource(request.ResourceType!, request.ResourceId!)
                : await ByUserAsync(request.UserId, cancellationToken);

            var total = await customer.CountAsync(cancellationToken)
                        + await admin.CountAsync(cancellationToken)
                        + await employee.CountAsync(cancellationToken);

            // Each source contributes its own top (offset + limit) rows; the global page is a subset of
            // their union, so the merge below never needs a row that was not fetched.
            var window = request.Offset + request.Limit;

            var customerRows = await customer
                .OrderByDescending(a => a.OccurredOn).ThenBy(a => a.Id)
                .Take(window)
                .AsNoTracking()
                .Select(a => a.MapToTimelineEntry())
                .ToListAsync(cancellationToken);

            var adminRows = await admin
                .OrderByDescending(a => a.OccurredOn).ThenBy(a => a.Id)
                .Take(window)
                .AsNoTracking()
                .Select(a => a.MapToTimelineEntry())
                .ToListAsync(cancellationToken);

            var employeeRows = await employee
                .OrderByDescending(a => a.CreatedOn).ThenBy(a => a.Id)
                .Take(window)
                .AsNoTracking()
                .Select(a => a.MapToTimelineEntry(EmployeeActionLabel(a.Action)))
                .ToListAsync(cancellationToken);

            var page = Page(customerRows.Concat(adminRows).Concat(employeeRows), request.Offset, request.Limit);

            return page.MapToDto(total, request);
        }

        private (IQueryable<CustomerActionAudit>, IQueryable<AdminActionAudit>, IQueryable<EmployeeActionAudit>) ByResource(
            string resourceType, string resourceId)
        {
            var customer = customerActionAuditRepository.GetQueryable()
                .Where(a => a.ResourceType == resourceType && a.ResourceId == resourceId);
            var admin = adminActionAuditRepository.GetQueryable()
                .Where(a => a.ResourceType == resourceType && a.ResourceId == resourceId);
            var employee = resourceType == nameof(Order)
                ? employeeActionAuditRepository.GetQueryable().Where(a => a.OrderId == resourceId)
                : employeeActionAuditRepository.GetQueryable().Where(a => false);

            return (customer, admin, employee);
        }

        // Validation proves the admin's account scope; resource ids are derived from that user.
        private async Task<(IQueryable<CustomerActionAudit>, IQueryable<AdminActionAudit>, IQueryable<EmployeeActionAudit>)> ByUserAsync(
            string userId, CancellationToken cancellationToken)
        {
            var provenOrderIds = await ProvenOrderIds(customerActionAuditRepository.GetQueryableForUser(userId), userId)
                .ToListAsync(cancellationToken);
            var orderIds = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.UserId == userId || provenOrderIds.Contains(o.Id))
                .OrderByDescending(o => o.CreatedOn)
                .Take(RecentResourceCap)
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);
            var disputeIds = await disputeRepository.GetQueryableForOwner(userId)
                .OrderByDescending(d => d.CreatedOn)
                .Take(RecentResourceCap)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);
            var membershipIds = await userMembershipRepository.GetQueryable()
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CreatedOn)
                .Take(RecentResourceCap)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            var customer = customerActionAuditRepository.GetQueryableForUser(userId);
            var admin = adminActionAuditRepository.GetQueryableIgnoringTenant()
                .Where(a => a.ResourceId != null && (
                    (a.ResourceType == nameof(User) && a.ResourceId == userId)
                    || (a.ResourceType == nameof(Order) && orderIds.Contains(a.ResourceId))
                    || (a.ResourceType == nameof(Dispute) && disputeIds.Contains(a.ResourceId))
                    || (a.ResourceType == nameof(UserMembership) && membershipIds.Contains(a.ResourceId))));
            var employee = employeeActionAuditRepository.GetQueryableIgnoringTenant()
                .Where(a => orderIds.Contains(a.OrderId));

            return (customer, admin, employee);
        }
    }

    /// <summary>
    /// The orders a subject's own successful acts named, newest act first, capped at
    /// <see cref="RecentResourceCap"/>. An erasure blanks <c>Order.UserId</c>, so this is how an erased
    /// subject's orders are still theirs to the timeline and the incident file; a refused act proves
    /// nothing — its order id is whatever the caller typed. The cap is taken after the ordering so which
    /// orders an erased subject keeps is never whichever ids the database returned first.
    /// </summary>
    public static IQueryable<string> ProvenOrderIds(IQueryable<CustomerActionAudit> customerRows, string userId) =>
        customerRows
            .Where(a => a.UserId == userId && a.Success && a.ResourceType == nameof(Order) && a.ResourceId != null)
            .GroupBy(a => a.ResourceId!)
            .Select(g => new { OrderId = g.Key, LastActedOn = g.Max(a => a.OccurredOn) })
            .OrderByDescending(x => x.LastActedOn)
            .ThenBy(x => x.OrderId)
            .Take(RecentResourceCap)
            .Select(x => x.OrderId);

    public static IReadOnlyList<TimelineEntryDto> Page(IEnumerable<TimelineEntryDto> merged, int offset, int limit) =>
        merged
            .OrderByDescending(e => e.OccurredOn)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .Skip(offset)
            .Take(limit)
            .ToList();

    /// <summary>
    /// The employee table stores its act as an enum; the timeline speaks the same dotted labels the
    /// other two tables carry, so one column reads the same whatever the source. No fallback arm: a new
    /// enum member must be named here, or it would reach the timeline under a label no catalogue knows.
    /// </summary>
    public static string EmployeeActionLabel(EmployeeAuditAction action) => action switch
    {
        EmployeeAuditAction.CoverRequested => "employee.order.cover_requested",
        EmployeeAuditAction.OrderDropped => "employee.order.dropped",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };
}
