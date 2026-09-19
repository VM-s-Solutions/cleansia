using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.AppServices.Shared.DTOs.Enums;

/// <summary>
/// Which audit table a timeline row came from (ADR-0062 D6). Values cross the wire as integers, so a
/// member may be added but never reordered or renumbered.
/// </summary>
[SwaggerEnumAsInt]
public enum TimelineSource
{
    Customer = 1,
    Admin = 2,
    Employee = 3,
}
