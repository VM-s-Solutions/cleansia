namespace Cleansia.Core.AppServices.Features.TenantSettings;

public sealed class BoolTenantSetting(string key, string category, bool @default)
    : TenantSettingDefinition<bool>(key, category, @default)
{
    public override TenantSettingValueType ValueType => TenantSettingValueType.Bool;

    public override int? Min => null;

    public override int? Max => null;

    public override bool TryParse(string? value, out bool parsed) => bool.TryParse(value?.Trim(), out parsed);

    public override string Format(bool value) => value ? "true" : "false";
}
