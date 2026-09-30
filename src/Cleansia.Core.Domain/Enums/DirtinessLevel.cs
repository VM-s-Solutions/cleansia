using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// How dirty the home is, as the customer stated it at booking. Normal is zero so a client that omits
/// the field books at Normal (owner ruling 2026-09-28). The integers are on the wire: append only.
/// </summary>
[SwaggerEnumAsInt]
public enum DirtinessLevel
{
    Normal = 0,
    Increased = 1,
    Heavy = 2,
}
