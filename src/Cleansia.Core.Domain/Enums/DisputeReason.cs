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
    Other = 7,
    // Bank-originated chargeback (ADR-0006 D4). Distinct from the customer-asserted
    // UnauthorizedCharge: this is the issuing bank pulling funds, not a customer claim.
    Chargeback = 8
}
