using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// T-0124 (PERF-IDA-01, folds in PERF-IDA-05) — EF-MODEL-METADATA guard for the identity-lookup
/// indexes on <see cref="User"/>.
///
/// THE HOLE: <c>UserEntityConfiguration</c> configured <c>Email</c>, <c>PhoneNumber</c>,
/// <c>GoogleId</c>, <c>ConfirmationCode</c> and <c>ResetPasswordCode</c> as properties but declared
/// ZERO <c>HasIndex</c>, so every identity query in <c>UserRepository</c> (login / register / reset /
/// confirm / profile-load) did a SEQUENTIAL SCAN of the Users table; cost grows linearly with the user
/// base. PERF-IDA-05: there was also no DB-level uniqueness on <c>Email</c> — uniqueness was only an
/// app-code pre-check (a TOCTOU race).
///
/// THE FIX (asserted here): a UNIQUE index over <c>Email</c> (citext ⇒ case-insensitive unique, closes
/// the race AND makes existence checks index-backed) + NON-UNIQUE indexes over <c>PhoneNumber</c>,
/// <c>ConfirmationCode</c>, <c>ResetPasswordCode</c>, <c>GoogleId</c>, with the four nullable lookup
/// columns FILTERED/PARTIAL (<c>WHERE "Col" IS NOT NULL</c>) so null rows are not indexed.
///
/// These spin a REAL <see cref="CleansiaDbContext"/> (so <c>OnModelCreating</c> + the entity config's
/// <c>HasIndex(...)</c> calls actually run) and introspect <see cref="IEntityType.GetIndexes"/> — same
/// model-introspection harness style the membership index tests use. Written TEST-FIRST: RED before the
/// <c>HasIndex</c> calls exist in <c>UserEntityConfiguration</c>, GREEN once they land.
///
/// Each AC1–AC3 maps to a metadata assertion below. AC4 (no behavioral regression) is covered by the
/// existing repository / handler tests staying green — no query body is touched by this ticket.
/// </summary>
public sealed class UserIdentityLookupIndexTests
{
    /// <summary>
    /// Build the real <see cref="CleansiaDbContext"/> model (no DB connection needed — index metadata is
    /// declared in <c>OnModelCreating</c> / the entity configs) and return the <see cref="User"/> entity
    /// type so the tests can read <see cref="IEntityType.GetIndexes"/>.
    /// </summary>
    private static IEntityType GetUserEntityType()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var ctx = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

        var entityType = ctx.Model.FindEntityType(typeof(User));
        Assert.NotNull(entityType);
        return entityType!;
    }

    /// <summary>
    /// True when <paramref name="index"/> covers exactly the single property <paramref name="propertyName"/>
    /// (a single-column index over that column — not a composite that happens to include it).
    /// </summary>
    private static bool IsSingleColumnIndexOn(IIndex index, string propertyName) =>
        index.Properties.Count == 1 && index.Properties[0].Name == propertyName;

    /// <summary>
    /// True when <paramref name="index"/> covers exactly the ordered properties given (a composite index).
    /// </summary>
    private static bool IsCompositeIndexOn(IIndex index, params string[] propertyNames) =>
        index.Properties.Count == propertyNames.Length
        && index.Properties.Select(p => p.Name).SequenceEqual(propertyNames);

    // ── AC1 (PR review #6, S8) — exactly ONE unique index over (TenantId, Email) ──
    // Uniqueness is PER-TENANT: User is an ITenantEntity and the app-layer checks run inside the tenant
    // filter, so a global Email unique index let tenant B 500 on tenant A's email (cross-tenant oracle)
    // and barred the same person across tenants. citext keeps the Email component case-insensitive.
    [Fact]
    public void Email_HasExactlyOneTenantScopedUniqueIndex()
    {
        var user = GetUserEntityType();

        var emailIndexes = user.GetIndexes()
            .Where(ix => IsCompositeIndexOn(ix, nameof(User.TenantId), nameof(User.Email)))
            .ToList();

        Assert.Single(emailIndexes);
        Assert.True(
            emailIndexes[0].IsUnique,
            "User email uniqueness must be the COMPOSITE (TenantId, Email) UNIQUE index (PR review #6 / "
            + "S8): per-tenant uniqueness matches the tenant-filtered app pre-check and closes the "
            + "same-tenant register/update TOCTOU race without leaking across tenants.");

        // And there must be NO global single-column unique index on Email left behind.
        Assert.DoesNotContain(
            user.GetIndexes(),
            ix => IsSingleColumnIndexOn(ix, nameof(User.Email)) && ix.IsUnique);
    }

    // ── AC2 — non-unique indexes on the four nullable lookup columns ──
    [Theory]
    [InlineData(nameof(User.PhoneNumber))]
    [InlineData(nameof(User.ConfirmationCode))]
    [InlineData(nameof(User.ResetPasswordCode))]
    [InlineData(nameof(User.GoogleId))]
    public void LookupColumn_HasNonUniqueIndex(string propertyName)
    {
        var user = GetUserEntityType();

        var index = user.GetIndexes().SingleOrDefault(ix => IsSingleColumnIndexOn(ix, propertyName));

        Assert.NotNull(index);
        Assert.False(
            index!.IsUnique,
            $"User.{propertyName} must carry a NON-UNIQUE index (it backs the identity lookups but is "
            + "not unique — multiple rows can legitimately share null / the same token shape).");
    }

    // ── AC2 — the nullable lookup columns are FILTERED/PARTIAL (WHERE "Col" IS NOT NULL) ──
    [Theory]
    [InlineData(nameof(User.PhoneNumber))]
    [InlineData(nameof(User.ConfirmationCode))]
    [InlineData(nameof(User.ResetPasswordCode))]
    [InlineData(nameof(User.GoogleId))]
    public void NullableLookupColumn_IndexIsFiltered_OnNotNull(string propertyName)
    {
        var user = GetUserEntityType();

        var index = user.GetIndexes().SingleOrDefault(ix => IsSingleColumnIndexOn(ix, propertyName));
        Assert.NotNull(index);

        var filter = index!.GetFilter();
        Assert.False(
            string.IsNullOrWhiteSpace(filter),
            $"User.{propertyName} is nullable — its index MUST be filtered/partial so null rows are not "
            + "indexed (HasFilter(\"\\\"{propertyName}\\\" IS NOT NULL\")).");
        Assert.Contains("IS NOT NULL", filter!, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC1/AC3 — the unique Email guarantee is real DB-level metadata, not an app-only pre-check ──
    [Fact]
    public void Email_UniqueIndex_IsTheDbLevelGuarantee_NotJustTheAppPreCheck()
    {
        var user = GetUserEntityType();

        var emailUnique = user.GetIndexes()
            .Any(ix => IsCompositeIndexOn(ix, nameof(User.TenantId), nameof(User.Email)) && ix.IsUnique);

        Assert.True(
            emailUnique,
            "PERF-IDA-05 + PR review #6: a UNIQUE DB index over (TenantId, Email) is the real guarantee — "
            + "the second same-tenant same-email insert must raise a unique violation. The "
            + "ExistsWithEmailAsync app pre-check stays as a fast-path UX message, but it is NOT the "
            + "constraint.");
    }

    /// <summary>Mirrors the membership index tests' tenant provider (null ⇒ anonymous / no JWT).</summary>
    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
