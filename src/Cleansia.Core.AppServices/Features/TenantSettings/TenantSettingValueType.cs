using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.AppServices.Features.TenantSettings;

/// <summary>
/// The type a setting's value is typed and edited as. Values cross the wire as integers, so a member
/// may be added but never reordered or renumbered.
/// </summary>
[SwaggerEnumAsInt]
public enum TenantSettingValueType
{
    Int = 1,
    Bool = 2,
    Email = 3,
}
