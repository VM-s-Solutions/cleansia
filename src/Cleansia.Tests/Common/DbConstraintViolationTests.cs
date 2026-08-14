using Cleansia.Core.AppServices.Common;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Common;

/// <summary>
/// <see cref="DbConstraintViolation"/> classifies a <see cref="DbUpdateException"/> by Postgres SQLSTATE
/// so a handler can map a lost race to a business refusal instead of a 500. It duck-types the inner
/// exception's public <c>SqlState</c> — AppServices carries no Npgsql reference — and walks the whole
/// inner chain, because EF wraps the provider exception at varying depth.
///
/// It classifies by SQLSTATE alone, deliberately. A constraint-NAMED variant existed until 2026-08-14
/// and was removed: every caller wraps a deliberate flush of ONE insert, so only one unique index can
/// speak, and the name it matched on bought nothing while costing a second type, a constants file and a
/// cross-assembly pin to keep them aligned.
/// </summary>
public class DbConstraintViolationTests
{
    private const string EmailIndex = "IX_Users_TenantId_Email";
    private const string OtherIndex = "IX_Carts_UserId";

    private static DbUpdateException Wrap(Exception inner) => new("commit failed", inner);

    [Fact]
    public void IsUniqueViolation_Matches_23505_Whatever_The_Constraint_Is_Called()
    {
        Assert.True(DbConstraintViolation.IsUniqueViolation(Wrap(new FakePostgresException("23505", EmailIndex))));
        Assert.True(DbConstraintViolation.IsUniqueViolation(Wrap(new FakePostgresException("23505", OtherIndex))));
        Assert.True(DbConstraintViolation.IsUniqueViolation(Wrap(new FakePostgresException("23505", null))));
    }

    // Fail closed on anything that is not a unique violation: the exception keeps propagating rather
    // than being mapped to a business error it does not mean.
    [Fact]
    public void IsUniqueViolation_Rejects_Anything_That_Is_Not_23505()
    {
        Assert.False(DbConstraintViolation.IsUniqueViolation(Wrap(new FakePostgresException("23503", EmailIndex))));
        Assert.False(DbConstraintViolation.IsUniqueViolation(Wrap(new InvalidOperationException("23505"))));
    }

    // Postgres raises 23001 for an EXPLICIT ON DELETE RESTRICT and 23503 for a NO ACTION FK. Both mean
    // "a row references the row being deleted", so both must reach the in-use refusal — checking only
    // 23503 lets the 23001 from the explicit RESTRICT catalog FKs surface as a raw 500.
    [Fact]
    public void IsForeignKeyViolation_Matches_Both_23503_And_23001()
    {
        Assert.True(DbConstraintViolation.IsForeignKeyViolation(Wrap(new FakePostgresException("23503", OtherIndex))));
        Assert.True(DbConstraintViolation.IsForeignKeyViolation(Wrap(new FakePostgresException("23001", null))));
        Assert.False(DbConstraintViolation.IsForeignKeyViolation(Wrap(new FakePostgresException("23505", null))));
    }

    [Fact]
    public void It_Walks_The_Whole_Inner_Chain()
    {
        var exception = Wrap(new InvalidOperationException("wrapper", new FakePostgresException("23505", EmailIndex)));

        Assert.True(DbConstraintViolation.IsUniqueViolation(exception));
    }

    /// <summary>
    /// Npgsql's <c>PostgresException</c> is not referenced from this layer; the classifier duck-types
    /// the public <c>SqlState</c> string, so a stand-in carrying it exercises the real read path.
    /// </summary>
    private sealed class FakePostgresException(string sqlState, string? constraintName)
        : Exception("postgres")
    {
        public string SqlState { get; } = sqlState;

        public string? ConstraintName { get; } = constraintName;
    }
}
