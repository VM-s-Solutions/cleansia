using System.Text.Json;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Why an administrator took the calling cleaner off a job: the written reason of the latest reassignment
/// that removed them, read from its audit row. The reason stays on the server — the
/// <c>order.assignment_revoked</c> notice carries only the order, and a cleaner opening it asks here.
/// </summary>
public class GetMyAssignmentRemoval
{
    public record Query(string OrderId) : IQuery<Response>;

    public record Response(string OrderId, string Reason, DateTimeOffset RemovedOn);

    public class Validator : AbstractValidator<Query>
    {
        private readonly IOrderAccessService _orderAccessService;
        private readonly IAdminActionAuditRepository _adminActionAuditRepository;

        public Validator(IOrderAccessService orderAccessService, IAdminActionAuditRepository adminActionAuditRepository)
        {
            _orderAccessService = orderAccessService;
            _adminActionAuditRepository = adminActionAuditRepository;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(26)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .MustAsync(async (orderId, cancellationToken) =>
                    await FindAsync(_orderAccessService, _adminActionAuditRepository, orderId, cancellationToken) is not null)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }
    }

    public class Handler(
        IOrderAccessService orderAccessService,
        IAdminActionAuditRepository adminActionAuditRepository) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var removal = await FindAsync(orderAccessService, adminActionAuditRepository, query.OrderId, cancellationToken);
            if (removal is null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(query.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            return BusinessResult.Success(new Response(query.OrderId, removal.Reason!, removal.OccurredOn));
        }
    }

    private static readonly string ReassignAction = AuditActionDescriptor.For(typeof(AdminReassignOrder.Command)).AdminAction;

    private static async Task<AdminActionAudit?> FindAsync(
        IOrderAccessService orderAccessService,
        IAdminActionAuditRepository adminActionAuditRepository,
        string orderId,
        CancellationToken cancellationToken)
    {
        var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
        if (string.IsNullOrEmpty(employeeId))
        {
            return null;
        }

        var reassignments = await adminActionAuditRepository.GetSucceededForResourceAsync(
            ReassignAction, "Order", orderId, cancellationToken);
        return reassignments.FirstOrDefault(row =>
            !string.IsNullOrWhiteSpace(row.Reason)
            && row.BeforeJson is not null
            && JsonSerializer.Deserialize<AdminReassignOrder.CrewSnapshot>(row.BeforeJson, JsonSerializerOptions.Web)?.EmployeeId == employeeId);
    }
}
