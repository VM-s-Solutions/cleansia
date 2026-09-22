namespace Cleansia.Core.AppServices.Features.TenantSettings;

/// <summary>
/// What the audit trail keeps of a setting write: the key and the stored value, which is a number or a
/// switch the company chose and never personal data. A null value is "no row" — the default in effect.
/// </summary>
public record TenantSettingSnapshot(string Key, string? Value);
