using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum ReceivableKind
{
    CashCancellationFee = 1,
    Lockout = 2,
    UnpaidCash = 3,
    TopUp = 4
}
