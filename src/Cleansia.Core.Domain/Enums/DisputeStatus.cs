using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum DisputeStatus
{
    Pending = 1,
    UnderReview = 2,
    WaitingForResponse = 3,
    Resolved = 4,
    Closed = 5,
    Escalated = 6
}
