using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.AppServices.Features.TenantSettings.DTOs;

namespace Cleansia.Core.AppServices.Mappers;

public static class TenantSettingMappers
{
    public static TenantSettingDto MapToDto(this TenantSettingDefinition definition, string? storedValue)
    {
        return new TenantSettingDto(
            definition.Key,
            definition.Category,
            definition.ValueType,
            definition.Min,
            definition.Max,
            definition.DefaultValue,
            definition.Canonicalize(storedValue) ?? definition.DefaultValue,
            storedValue is not null);
    }
}
