using Cleansia.Core.AppServices.Common;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Common;

/// <summary>
/// <see cref="DbConstraintViolation.IsUniqueViolationOn"/> is the constraint-NAMED form required by
/// ADR-0050 §D2: a commit that stages more than one row can raise 23505 from any unique index in it, so
/// a writer that answers "that email is taken" to a bare SQLSTATE would report the wrong cause for an
/// unrelated collision. The name is read from Npgsql's <c>PostgresException.ConstraintName</c> by the
/// same duck-typing the SQLSTATE read uses (AppServices carries no Npgsql reference), and an absent or
/// mismatched name answers false so the exception keeps propagating rather than being mapped to a
/// business error it does not mean.
/// </summary>
public class DbConstraintViolationTests
{
    private const string EmailIndex = "IX_Users_TenantId_Email";
    private const string OtherIndex = "IX_Carts_UserId";

    private static DbUpdateException Wrap(Exception inner) => new("commit failed", inner);

    [Fact]
    public void IsUniqueViolationOn_Matches_The_Named_Constraint()
    {
        var exception = Wrap(new FakePostgresException("23505", EmailIndex));

        Assert.True(DbConstraintViolation.IsUniqueViolationOn(exception, EmailIndex));
    }

    [Fact]
    public void IsUniqueViolationOn_Rejects_A_Unique_Violation_On_A_DIFFERENT_Constraint()
    {
        var exception = Wrap(new FakePostgresException("23505", OtherIndex));

        Assert.False(DbConstraintViolation.IsUniqueViolationOn(exception, EmailIndex));
    }

    // Fail closed: a provider that names no constraint leaves the writer unable to say WHICH uniqueness
    // was violated, and a guessed answer is worse than the 500.
    [Fact]
    public void IsUniqueViolationOn_Rejects_A_Unique_Violation_With_No_Constraint_Name()
    {
        Assert.False(DbConstraintViolation.IsUniqueViolationOn(Wrap(new FakePostgresException("23505", null)), EmailIndex));
        Assert.False(DbConstraintViolation.IsUniqueViolationOn(Wrap(new InvalidOperationException("23505")), EmailIndex));
    }

    [Fact]
    public void IsUniqueViolationOn_Rejects_A_ForeignKey_Violation_Naming_The_Same_Constraint()
    {
        var exception = Wrap(new FakePostgresException("23503", EmailIndex));

        Assert.False(DbConstraintViolation.IsUniqueViolationOn(exception, EmailIndex));
    }

    [Fact]
    public void IsUniqueViolationOn_Walks_The_Whole_Inner_Chain()
    {
        var exception = Wrap(new InvalidOperationException("wrapper", new FakePostgresException("23505", EmailIndex)));

        Assert.True(DbConstraintViolation.IsUniqueViolationOn(exception, EmailIndex));
    }

    // The un-named overloads keep answering on SQLSTATE alone — the shipped callers
    // (GenerateInvoice, AssignInvoiceVariableSymbol, DeleteService/DeletePackage) stage one row and
    // must not start depending on a constraint name.
    [Fact]
    public void The_Unnamed_Overloads_Are_Unchanged_By_The_Constraint_Name()
    {
        Assert.True(DbConstraintViolation.IsUniqueViolation(Wrap(new FakePostgresException("23505", OtherIndex))));
        Assert.True(DbConstraintViolation.IsUniqueViolation(Wrap(new FakePostgresException("23505", null))));
        Assert.True(DbConstraintViolation.IsForeignKeyViolation(Wrap(new FakePostgresException("23503", OtherIndex))));
        Assert.True(DbConstraintViolation.IsForeignKeyViolation(Wrap(new FakePostgresException("23001", null))));
        Assert.False(DbConstraintViolation.IsUniqueViolation(Wrap(new FakePostgresException("23503", EmailIndex))));
    }

    /// <summary>
    /// Npgsql's <c>PostgresException</c> is not referenced from this layer; the classifier duck-types
    /// the two public string properties, so a stand-in carrying them exercises the real read path.
    /// </summary>
    private sealed class FakePostgresException(string sqlState, string? constraintName)
        : Exception("postgres")
    {
        public string SqlState { get; } = sqlState;

        public string? ConstraintName { get; } = constraintName;
    }
}
