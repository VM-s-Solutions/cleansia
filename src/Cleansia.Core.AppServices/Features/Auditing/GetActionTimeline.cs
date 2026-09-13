using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Auditing;

/// <summary>
/// One <c>OccurredOn DESC</c> page over the three audit tables (ADR-0062 D6), keyed by a user or by a
/// resource. By user: the customer's own rows, the admin rows on the user or on their orders, disputes
/// and memberships, and the cleaner rows on their orders. By resource: every row of the three tables
/// that names it — which is the only way a guest act (<c>UserId</c> null) is ever reached.
/// </summary>
public class GetActionTimeline
{
    public const int MaxLimit = 100;

    /// <summary>
    /// The admin and employee arms match the user through the ids of what they own; the set is capped
    /// at the most recent rows so the <c>IN (...)</c> list cannot grow with the account.
    /// </summary>
    public const int RecentResourceCap = 1000;

    public record Query(
        string? UserId = null,
        string? ResourceType = null,
        string? ResourceId = null,
        int Offset = 0,
        int Limit = 20) : IQuery<PagedData<TimelineEntryDto>>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x)
                .Must(KeyedByExactlyOneOfUserOrResource)
                .WithMessage(BusinessErrorMessage.TimelineFilterRequired)
                .WithName(nameof(Query.UserId));

            RuleFor(x => x.Offset)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.Limit)
                .InclusiveBetween(1, MaxLimit)
                .WithMessage(BusinessErrorMessage.MustBePositive);
        }

        private static bool KeyedByExactlyOneOfUserOrResource(Query query)
        {
            var hasUser = !string.IsNullOrWhiteSpace(query.UserId);
            var hasType = !string.IsNullOrWhiteSpace(query.ResourceType);
            var hasId = !string.IsNullOrWhiteSpace(query.ResourceId);

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
        : IQueryHandler<Query, PagedData<TimelineEntryDto>>
    {
        public async Task<BusinessResult<PagedData<TimelineEntryDto>>> Handle(Query request, CancellationToken cancellationToken)
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

            return BusinessResult.Success(new PagedData<TimelineEntryDto>(
                PageNumber: request.Offset / request.Limit + 1,
                PageSize: request.Limit,
                Total: total,
                Data: page));
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

        private async Task<(IQueryable<CustomerActionAudit>, IQueryable<AdminActionAudit>, IQueryable<EmployeeActionAudit>)> ByUserAsync(
            string userId, CancellationToken cancellationToken)
        {
            var orderIds = await orderRepository.GetQueryable()
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedOn)
                .Take(RecentResourceCap)
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);
            var disputeIds = await disputeRepository.GetQueryable()
                .Where(d => d.UserId == userId)
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

            var customer = customerActionAuditRepository.GetQueryable()
                .Where(a => a.UserId == userId);
            var admin = adminActionAuditRepository.GetQueryable()
                .Where(a => a.ResourceId != null && (
                    (a.ResourceType == nameof(User) && a.ResourceId == userId)
                    || (a.ResourceType == nameof(Order) && orderIds.Contains(a.ResourceId))
                    || (a.ResourceType == nameof(Dispute) && disputeIds.Contains(a.ResourceId))
                    || (a.ResourceType == nameof(UserMembership) && membershipIds.Contains(a.ResourceId))));
            var employee = employeeActionAuditRepository.GetQueryable()
                .Where(a => orderIds.Contains(a.OrderId));

            return (customer, admin, employee);
        }
    }

    public static IReadOnlyList<TimelineEntryDto> Page(IEnumerable<TimelineEntryDto> merged, int offset, int limit) =>
        merged
            .OrderByDescending(e => e.OccurredOn)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .Skip(offset)
            .Take(limit)
            .ToList();

    /// <summary>
    /// The employee table stores its act as an enum; the timeline speaks the same dotted labels the
    /// other two tables carry, so one column reads the same whatever the source.
    /// </summary>
    public static string EmployeeActionLabel(EmployeeAuditAction action) => action switch
    {
        EmployeeAuditAction.CoverRequested => "employee.order.cover_requested",
        EmployeeAuditAction.OrderDropped => "employee.order.dropped",
        _ => $"employee.{action.ToString().ToLowerInvariant()}",
    };
}
