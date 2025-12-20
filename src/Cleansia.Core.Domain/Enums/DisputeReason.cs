using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum DisputeReason
{
    QualityIssue = 1,
    ServiceNotProvided = 2,
    ServiceIncomplete = 3,
    DamagedProperty = 4,
    UnauthorizedCharge = 5,
    IncorrectAmount = 6,
    Other = 7
}
