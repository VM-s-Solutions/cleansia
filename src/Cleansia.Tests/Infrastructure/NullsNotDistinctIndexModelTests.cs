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


    /// <summary>
    /// Unique indexes whose nullable column is removed from the index by a FILTER, so declaring
    /// NULLS NOT DISTINCT would change nothing — plus the one deliberate backstop.
    /// </summary>
    private static readonly HashSet<string> NullsDistinctIsFine = new(StringComparer.Ordinal)
    {
        // Filtered "VariableSymbol" IS NOT NULL, so no indexed row can hold a null in it.
        "EmployeeInvoice (VariableSymbol)",
        // Filtered "RecurringTemplateId" IS NOT NULL — the nullable column cannot be null in an indexed row.
        "Order (RecurringTemplateId, CleaningDateTime)",
        // The documented backstop behind GetActiveForUserAsync, deliberately left nulls-distinct.
        "UserMembership (TenantId, UserId)",
    };

    /// <summary>
    /// Unique indexes that contain an unfiltered nullable column and do NOT declare
    /// <c>.AreNullsDistinct(false)</c>.
    ///
    /// <para><b>These are gaps, not decisions.</b> Every one of them enforces nothing whenever that
    /// column is null — which for the <c>TenantId</c> ones is the platform's entire single-tenant
    /// deployment, i.e. production today. They are recorded so the sweep below can be fail-CLOSED
    /// about everything else while the owner rules on them. The list may only ever SHRINK.</para>
    ///
    /// <para>The sharpest is <c>LoyaltyTransaction (TenantId, IdempotencyKey)</c>: its own
    /// configuration comment calls it "the atomic backstop" against a double grant, and the admin UI
    /// mints a fresh idempotency token per click — so a double-clicked grant doubles a customer's
    /// points, their tier, and therefore a real discount.</para>
    /// </summary>
    private static readonly HashSet<string> KnownUnenforced = new(StringComparer.Ordinal)
    {
        "EmployeePayConfig (EmployeeId, ServiceId, PackageId)",
        "FeatureFlag (Name, Scope, ScopeValue)",
        "LoyaltyTierConfig (TenantId, Tier)",
        "LoyaltyTransaction (TenantId, IdempotencyKey)",
        "PromoCode (TenantId, Code)",
        "ReferralCode (TenantId, Code)",
        "TenantConfiguration (TenantId, Key)",
    };

    private static string Describe(IEntityType entity, IIndex index) =>
        $"{entity.ClrType.Name} ({string.Join(", ", index.Properties.Select(pr => pr.Name))})";

    /// <summary>
    /// Every unique index in the model, not the five somebody remembered to list.
    ///
    /// <para>The theory above is a hand-written roster, and that shape fails OPEN — a new unique
    /// index over a nullable column is not weakly covered by it, it is invisible to it. Two indexes
    /// added in this very commit would have been, and <c>LoyaltyTransaction</c> already was. This
    /// walks the model instead: any unique index carrying a nullable column must either declare
    /// NULLS NOT DISTINCT, be filtered so the null cannot appear, or be named above.</para>
    /// </summary>
    [Fact]
    public void No_Unique_Index_Over_A_Nullable_Column_Silently_Fails_To_Enforce()
    {
        using var ctx = NewContext();

        var offenders = new List<string>();

        foreach (var entity in ctx.Model.GetEntityTypes())
        foreach (var index in entity.GetIndexes().Where(ix => ix.IsUnique))
        {
            if (!index.Properties.Any(pr => pr.IsNullable))
            {
                continue;
            }

            if (index.GetAreNullsDistinct() == false)
            {
                continue;
            }

            var description = Describe(entity, index);
            if (NullsDistinctIsFine.Contains(description) || KnownUnenforced.Contains(description))
            {
                continue;
            }

            offenders.Add(description);
        }

        Assert.True(offenders.Count == 0,
            "PostgreSQL treats NULLs in a UNIQUE index as distinct, so these unique indexes enforce "
            + "nothing whenever their nullable column is null. Declare .AreNullsDistinct(false), "
            + "filter the null out, or record them with a reason:\n  "
            + string.Join("\n  ", offenders.OrderBy(x => x, StringComparer.Ordinal)));
    }

    /// <summary>
    /// Both recorded lists may only shrink. An entry that outlives its index turns the list into
    /// fiction, and fiction is what a roster degrades into when nothing checks it.
    /// </summary>
    [Fact]
    public void The_Recorded_Lists_Contain_Nothing_Stale()
    {
        using var ctx = NewContext();

        var live = ctx.Model.GetEntityTypes()
            .SelectMany(e => e.GetIndexes().Where(ix => ix.IsUnique).Select(ix => Describe(e, ix)))
            .ToHashSet(StringComparer.Ordinal);

        var stale = NullsDistinctIsFine.Concat(KnownUnenforced)
            .Where(d => !live.Contains(d))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0,
            "These recorded entries name indexes that no longer exist. Remove them:\n  "
            + string.Join("\n  ", stale));
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
