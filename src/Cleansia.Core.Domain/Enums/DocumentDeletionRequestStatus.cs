using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// Where a cleaner's request to remove one of their documents has got to.
///
/// <para><c>Pending</c> is the state that matters: the document is still ACTIVE and still counts
/// toward the cleaner's approval while an admin decides. A request changes nothing until it is
/// answered, so one that is never answered costs the cleaner nothing.</para>
/// </summary>
[SwaggerEnumAsInt]
public enum DocumentDeletionRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
