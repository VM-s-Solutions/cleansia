using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Tenancy;

/// <summary>
/// Whether a deactivated company still lets this account open a session on this audience (ADR-0064 D1).
/// Scoped and memoised: the issuing command asks first and refuses with the key it returns, then the
/// mint asks the same instance as an invariant, and the two share one <c>Tenants</c> read.
/// </summary>
public interface ICompanySignInGate
{
    /// <summary>The refusal key, or null when the account may sign in on <paramref name="audience"/>.</summary>
    Task<string?> RefusalForAsync(User user, string audience, CancellationToken cancellationToken);
}
