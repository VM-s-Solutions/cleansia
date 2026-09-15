namespace Cleansia.Core.AppServices.Features.TenantSettings.DTOs;

public record TenantSettingDto(
    string Key,
    string Category,
    TenantSettingValueType ValueType,
    int? Min,
    int? Max,
    string DefaultValue,
    string EffectiveValue,
    bool IsOverridden);
