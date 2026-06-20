using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Common;

/// <summary>
/// Classifies a <see cref="DbUpdateException"/> by the underlying Postgres SQLSTATE so handlers can map
/// a constraint violation to a deterministic business error instead of letting it surface as a 500
/// (S7/S7a). Detected provider-agnostically by duck-typing the inner exception's public <c>SqlState</c>
/// property: the AppServices layer deliberately carries no hard Npgsql reference, so Npgsql's
/// <c>PostgresException.SqlState</c> is read reflectively. The whole inner chain is walked because EF may
/// wrap the provider exception more than one level deep.
/// </summary>
public static class DbConstraintViolation
{
    // Postgres raises 23001 (restrict_violation) when an EXPLICIT ON DELETE RESTRICT fires, and 23503
    // (foreign_key_violation) for a NO ACTION FK. Both mean "a row references the row being deleted", so
    // both must map to the in-use business error — checking only 23503 would let the 23001 from our
    // explicit RESTRICT catalog FKs surface as a raw 500.
    private static readonly string[] ForeignKeyViolationStates = ["23503", "23001"];

    /// <summary>
    /// True when the exception was caused by a foreign-key/restrict constraint violation — here, an
    /// ON DELETE RESTRICT catalog reference rejecting the delete because a row references the row being
    /// deleted.
    /// </summary>
    public static bool IsForeignKeyViolation(DbUpdateException exception) =>
        HasSqlState(exception, ForeignKeyViolationStates);

    private static bool HasSqlState(DbUpdateException exception, string[] sqlStates)
    {
        for (Exception? inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            var state = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
            if (state is not null && Array.IndexOf(sqlStates, state) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
