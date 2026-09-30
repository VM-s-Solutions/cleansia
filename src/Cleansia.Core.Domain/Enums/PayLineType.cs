using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// What a pay row pays for: the job a cleaner completed, or the cleaner's share of a late-cancellation or
/// lockout fee the company collected on a job that did not happen (owner ruling 2026-09-28, decision 12).
/// The integers are on the wire: append only.
/// </summary>
[SwaggerEnumAsInt]
public enum PayLineType
{
    Job = 0,
    CancellationFeeShare = 1,
    LockoutFeeShare = 2,
}
