using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Loyalty;

[SwaggerEnumAsInt]
public enum PromoCodeType
{
    PercentDiscount = 1,
    FixedDiscount = 2,
}
