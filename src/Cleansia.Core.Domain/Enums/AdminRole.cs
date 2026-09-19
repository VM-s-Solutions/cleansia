using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// Which administrator an Administrator-profile account is. A second axis on the account, not a
/// profile: the profile answers "which audience?", the role answers "which administrator?". The
/// matrix is a lattice — Administrator ⊇ Manager ⊇ (Support ∪ Accountant) — so every admin permission
/// resolves to "the least role that has it, and everyone above".
/// </summary>
[SwaggerEnumAsInt]
public enum AdminRole
{
    Administrator = 1,
    Manager = 2,
    Support = 3,
    Accountant = 4,
}
