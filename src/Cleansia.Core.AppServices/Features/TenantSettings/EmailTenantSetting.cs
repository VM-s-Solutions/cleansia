namespace Cleansia.Core.AppServices.Features.TenantSettings;

/// <summary>
/// One e-mail address, stored trimmed and lower-cased. The shape rule is the platform's own
/// (<c>EmailAddress()</c> on every validator: exactly one <c>@</c> with something on both sides) plus
/// no whitespace, because an address with a space inside is not one SendGrid can deliver to. The
/// empty string is not a value: "unset" is the row's absence, so the writer refuses it and a reader
/// resolves no row to the empty default.
/// </summary>
public sealed class EmailTenantSetting(string key, string category, string @default)
    : TenantSettingDefinition<string>(key, category, @default)
{
    public const int MaxLength = 150;

    public override TenantSettingValueType ValueType => TenantSettingValueType.Email;

    public override int? Min => null;

    public override int? Max => null;

    public override bool TryParse(string? value, out string parsed)
    {
        parsed = string.Empty;
        var address = value?.Trim();
        if (string.IsNullOrEmpty(address) || address.Length > MaxLength || address.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = address.IndexOf('@');
        if (at <= 0 || at != address.LastIndexOf('@') || at == address.Length - 1)
        {
            return false;
        }

        parsed = address.ToLowerInvariant();
        return true;
    }

    public override string Format(string value) => value;
}
