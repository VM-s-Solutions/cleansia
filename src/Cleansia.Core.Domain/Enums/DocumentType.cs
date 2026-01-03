using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum DocumentType
{
    IdentityCard = 1,
    Passport = 2,
    DriversLicense = 3,
    WorkPermit = 4,
    Contract = 5,
    Certificate = 6,
    BankStatement = 7,
    TaxDocument = 8,
    InsuranceDocument = 9,
    Other = 10
}
