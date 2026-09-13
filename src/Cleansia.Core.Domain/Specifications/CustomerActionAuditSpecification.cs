using System.Linq.Expressions;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class CustomerActionAuditSpecification : BaseSpecification<string?>, ISpecification<CustomerActionAudit>
{
    public string? UserId { get; set; }
    public string? Action { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public DateTimeOffset? OccurredFrom { get; set; }
    public DateTimeOffset? OccurredTo { get; set; }
    public bool? Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ClientAudience { get; set; }

    public Expression<Func<CustomerActionAudit, bool>> SatisfiedBy()
    {
        Specification<CustomerActionAudit> specification = new TrueSpecification<CustomerActionAudit>();

        if (!string.IsNullOrEmpty(UserId))
        {
            specification &= new DirectSpecification<CustomerActionAudit>(x => x.UserId == UserId);
        }

        if (!string.IsNullOrEmpty(Action))
        {
            specification &= new DirectSpecification<CustomerActionAudit>(x => x.Action == Action);
        }

        if (!string.IsNullOrEmpty(ResourceType))
        {
            specification &= new DirectSpecification<CustomerActionAudit>(x => x.ResourceType == ResourceType);
        }

        if (!string.IsNullOrEmpty(ResourceId))
        {
            specification &= new DirectSpecification<CustomerActionAudit>(x => x.ResourceId == ResourceId);
        }

        if (OccurredFrom.HasValue)
        {
            specification &= new DirectSpecification<CustomerActionAudit>(x => x.OccurredOn >= OccurredFrom.Value);
        }

        if (OccurredTo.HasValue)
        {
            specification &= new DirectSpecification<CustomerActionAudit>(x => x.OccurredOn <= OccurredTo.Value);
        }

        if (Success.HasValue)
        {
            specification &= new DirectSpecification<CustomerActionAudit>(x => x.Success == Success.Value);
        }

        if (!string.IsNullOrEmpty(ErrorCode))
        {
            specification &= new DirectSpecification<CustomerActionAudit>(x =>
                x.ErrorCode != null && x.ErrorCode.Contains(ErrorCode));
        }

        if (!string.IsNullOrEmpty(ClientAudience))
        {
            specification &= new DirectSpecification<CustomerActionAudit>(x => x.ClientAudience == ClientAudience);
        }

        return specification.SatisfiedBy();
    }

    public static CustomerActionAuditSpecification Create(
        string? userId = null,
        string? action = null,
        string? resourceType = null,
        string? resourceId = null,
        DateTimeOffset? occurredFrom = null,
        DateTimeOffset? occurredTo = null,
        bool? success = null,
        string? errorCode = null,
        string? clientAudience = null)
    {
        return new CustomerActionAuditSpecification
        {
            UserId = userId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            OccurredFrom = occurredFrom,
            OccurredTo = occurredTo,
            Success = success,
            ErrorCode = errorCode,
            ClientAudience = clientAudience
        };
    }
}
