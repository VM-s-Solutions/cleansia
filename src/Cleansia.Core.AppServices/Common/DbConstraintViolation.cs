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

    private static readonly string[] UniqueViolationStates = ["23505"];

    /// <summary>
    /// True when the exception was caused by a unique-constraint violation — a concurrent (or
    /// hand-written) insert losing a race against a UNIQUE index. Callers that need this must FLUSH the
    /// insert themselves: the <c>UnitOfWorkPipelineBehavior</c> commit runs after the handler returns,
    /// so a try/catch around a merely-tracked <c>Add</c> catches nothing (S7b).
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        HasSqlState(exception, UniqueViolationStates);

    /// <summary>
    /// True when the exception was caused by a unique violation raised by the NAMED index or constraint.
    /// A commit that stages more than one row can raise 23505 from any unique index in it, so a writer
    /// that answers "that email is already taken" to a bare SQLSTATE reports the wrong cause for an
    /// unrelated collision; the name is what makes the mapping specific. Read from Npgsql's
    /// <c>PostgresException.ConstraintName</c> by the same duck-typing the SQLSTATE read uses, and NEVER
    /// from the driver's message text, which is not a contract. An absent or mismatched name answers
    /// false so the exception keeps propagating.
    /// </summary>
    public static bool IsUniqueViolationOn(DbUpdateException exception, string constraintName) =>
        HasSqlState(exception, UniqueViolationStates, constraintName);

    /// <summary>
    /// True when the exception was caused by a foreign-key/restrict constraint violation — here, an
    /// ON DELETE RESTRICT catalog reference rejecting the delete because a row references the row being
    /// deleted.
    /// </summary>
    public static bool IsForeignKeyViolation(DbUpdateException exception) =>
        HasSqlState(exception, ForeignKeyViolationStates);

    private static bool HasSqlState(DbUpdateException exception, string[] sqlStates, string? constraintName = null)
    {
        for (Exception? inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            var state = ReadStringProperty(inner, "SqlState");
            if (state is null || Array.IndexOf(sqlStates, state) < 0)
            {
                continue;
            }

            if (constraintName is null
                || string.Equals(ReadStringProperty(inner, "ConstraintName"), constraintName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadStringProperty(Exception exception, string propertyName) =>
        exception.GetType().GetProperty(propertyName)?.GetValue(exception) as string;
}
