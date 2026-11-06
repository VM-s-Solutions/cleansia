using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// How far a job is from the cleaner's stored home, in rough straight-line bands measured to the job's
/// snapped cell (<see cref="Orders.JobProximity.Band"/>). The set is frozen: the integers are on the wire,
/// and both generated mobile clients reject an unknown value and fail the whole response, so changing a
/// band or adding one means a new field, never a new value. → /decisions/adr-0079
/// </summary>
[SwaggerEnumAsInt]
public enum HomeDistanceBand
{
    Under3Km = 1,
    From3To6Km = 2,
    From6To10Km = 3,
    From10To20Km = 4,
    Over20Km = 5,
}
