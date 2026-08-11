using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// Single-tenant mode <b>is</b> <c>TenantId == null</c>, and PostgreSQL treats NULLs in a UNIQUE index
/// as distinct — so a tenant-scoped unique index that is the sole arbiter of a concurrent claim does
/// not fire at all in the platform's default deployment unless it is declared NULLS NOT DISTINCT
/// (ADR-0035 AM-6, ADR-0034 D1.3, ADR-0038 §D5.2).
///
/// <para>The option is one builder call and one annotation, invisible in a diff and silently
/// consequence-free in every SQLite test. This asserts it on each index that must have it, so dropping
/// it goes red here rather than in production under concurrency.</para>
/// </summary>
public sealed class NullsNotDistinctIndexModelTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public NullsNotDistinctIndexModelTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options, new TestUserSessionProvider("system", "system@cleansia.test"), new NullTenantProvider());
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }

    [Theory]
    [InlineData(typeof(FiscalCounter), new[] { "TenantId", "Year", "IssuerScope" })]
    [InlineData(typeof(EmployeePayoutDetails), new[] { "TenantId", "EmployeeId" })]
    [InlineData(typeof(PromoCodeRedemption), new[] { "TenantId", "PromoCodeId", "UserId", "SlotOrdinal" })]
    [InlineData(typeof(MembershipBenefitUsage),
        new[] { "TenantId", "UserId", "BenefitKind", "PeriodKey", "SlotOrdinal" })]
    [InlineData(typeof(User), new[] { "TenantId", "Email" })]
    public void A_Sole_Arbiter_Unique_Index_Is_Declared_Nulls_Not_Distinct(Type entityClrType, string[] columns)
    {
        using var ctx = NewContext();
        var index = FindIndex(ctx, entityClrType, columns);

        Assert.True(index.IsUnique, $"{entityClrType.Name} ({string.Join(", ", columns)}) must be UNIQUE.");
        Assert.False(
            index.GetAreNullsDistinct(),
            $"{entityClrType.Name} ({string.Join(", ", columns)}) is the sole arbiter of a concurrent "
            + "claim, so it must be declared .AreNullsDistinct(false) or it never fires when TenantId is null.");
    }

    /// <summary>
    /// The control: <c>UserMemberships</c> is a backstop behind the authoritative
    /// <c>GetActiveForUserAsync</c> assert, so it is deliberately left unset — PostgreSQL's default,
    /// nulls distinct. Without this the theory above could pass on a reader that answered <c>false</c>
    /// for every index.
    /// </summary>
    [Fact]
    public void A_Backstop_Unique_Index_Is_Left_Nulls_Distinct()
    {
        using var ctx = NewContext();
        var index = FindIndex(ctx, typeof(UserMembership), ["TenantId", "UserId"]);

        Assert.True(index.IsUnique);
        Assert.Null(index.GetAreNullsDistinct());
    }

    /// <summary>
    /// The release path restores capacity by flipping <c>IsActive</c>, which only frees the ordinal if
    /// the unique index is filtered to live rows.
    /// </summary>
    [Fact]
    public void The_Benefit_Slot_Index_Is_Filtered_To_Live_Rows()
    {
        using var ctx = NewContext();
        var index = FindIndex(
            ctx,
            typeof(MembershipBenefitUsage),
            ["TenantId", "UserId", "BenefitKind", "PeriodKey", "SlotOrdinal"]);

        Assert.Equal("\"IsActive\" = TRUE", index.GetFilter());
    }

    private static IIndex FindIndex(CleansiaDbContext ctx, Type entityClrType, string[] columns)
    {
        var entityType = ctx.Model.FindEntityType(entityClrType)!;
        var index = entityType.GetIndexes()
            .SingleOrDefault(ix => ix.Properties.Select(p => p.Name).SequenceEqual(columns));

        Assert.True(index is not null,
            $"{entityClrType.Name} has no index on ({string.Join(", ", columns)}).");
        return index!;
    }
}
