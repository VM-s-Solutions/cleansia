using System.Linq.Expressions;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class AdminActionAuditSpecification : BaseSpecification<string?>, ISpecification<AdminActionAudit>
{
    public string? ActorId { get; set; }
    public string? ActorEmail { get; set; }
    public string? Action { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public DateTimeOffset? OccurredFrom { get; set; }
    public DateTimeOffset? OccurredTo { get; set; }
    public bool? Success { get; set; }

    public Expression<Func<AdminActionAudit, bool>> SatisfiedBy()
    {
        Specification<AdminActionAudit> specification = new TrueSpecification<AdminActionAudit>();

        if (!string.IsNullOrEmpty(ActorId))
        {
            specification &= new DirectSpecification<AdminActionAudit>(x => x.ActorId == ActorId);
        }

        if (!string.IsNullOrEmpty(ActorEmail))
        {
            specification &= new DirectSpecification<AdminActionAudit>(x =>
                x.ActorEmail != null && x.ActorEmail.Contains(ActorEmail));
        }

        if (!string.IsNullOrEmpty(Action))
        {
            specification &= new DirectSpecification<AdminActionAudit>(x => x.Action == Action);
        }

        if (!string.IsNullOrEmpty(ResourceType))
        {
            specification &= new DirectSpecification<AdminActionAudit>(x => x.ResourceType == ResourceType);
        }

        if (!string.IsNullOrEmpty(ResourceId))
        {
            specification &= new DirectSpecification<AdminActionAudit>(x => x.ResourceId == ResourceId);
        }

        if (OccurredFrom.HasValue)
        {
            specification &= new DirectSpecification<AdminActionAudit>(x => x.OccurredOn >= OccurredFrom.Value);
        }

        if (OccurredTo.HasValue)
        {
            specification &= new DirectSpecification<AdminActionAudit>(x => x.OccurredOn <= OccurredTo.Value);
        }

        if (Success.HasValue)
        {
            specification &= new DirectSpecification<AdminActionAudit>(x => x.Success == Success.Value);
        }

        return specification.SatisfiedBy();
    }

    public static AdminActionAuditSpecification Create(
        string? actorId = null,
        string? actorEmail = null,
        string? action = null,
        string? resourceType = null,
        string? resourceId = null,
        DateTimeOffset? occurredFrom = null,
        DateTimeOffset? occurredTo = null,
        bool? success = null)
    {
        return new AdminActionAuditSpecification
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            OccurredFrom = occurredFrom,
            OccurredTo = occurredTo,
            Success = success
        };
    }
}
