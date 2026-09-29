using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum CashLedgerEntryKind
{
    Collection = 1,
    Remittance = 2,
    WriteOff = 3
}
