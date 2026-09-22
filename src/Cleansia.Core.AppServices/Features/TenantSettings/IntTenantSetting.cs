using System.Globalization;

namespace Cleansia.Core.AppServices.Features.TenantSettings;

public sealed class IntTenantSetting(string key, string category, int @default, int min, int max)
    : TenantSettingDefinition<int>(key, category, @default)
{
    public override TenantSettingValueType ValueType => TenantSettingValueType.Int;

    public override int? Min => min;

    public override int? Max => max;

    public override bool TryParse(string? value, out int parsed) =>
        int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
        && parsed >= min
        && parsed <= max;

    public override string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
