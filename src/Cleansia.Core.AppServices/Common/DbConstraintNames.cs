namespace Cleansia.Core.AppServices.Common;

/// <summary>
/// Database index/constraint names this layer maps to business errors via
/// <see cref="DbConstraintViolation.IsUniqueViolationOn"/>. The names are owned by
/// <c>Infra.Database</c>'s entity configurations, which AppServices deliberately cannot reference, so
/// each one is pinned against the EF model by a test (<c>UserIdentityLookupIndexTests</c>) — a rename
/// on the other side of that seam would otherwise turn a mapped business error back into a 500 with
/// nothing going red.
/// </summary>
public static class DbConstraintNames
{
    public const string UsersTenantIdEmailUnique = "IX_Users_TenantId_Email";
}
