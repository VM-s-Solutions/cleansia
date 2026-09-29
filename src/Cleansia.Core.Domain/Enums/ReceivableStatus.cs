using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum ReceivableStatus
{
    Open = 1,
    Paid = 2,
    WrittenOff = 3
}
