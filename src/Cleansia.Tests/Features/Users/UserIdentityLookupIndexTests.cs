using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// EF-MODEL-METADATA guard for the identity-lookup
/// indexes on <see cref="User"/>.
///
/// THE HOLE: <c>UserEntityConfiguration</c> configured <c>Email</c>, <c>PhoneNumber</c>,
/// <c>GoogleId</c>, <c>ConfirmationCode</c> and <c>ResetPasswordCode</c> as properties but declared
/// ZERO <c>HasIndex</c>, so every identity query in <c>UserRepository</c> (login / register / reset /
/// confirm / profile-load) did a SEQUENTIAL SCAN of the Users table; cost grows linearly with the user
/// base. There was also no DB-level uniqueness on <c>Email</c> — uniqueness was only an
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
/// Each metadata assertion is below. No behavioral regression is covered by the
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
            new FixedTenantProvider(TestTenants.Default));

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

    // Exactly ONE unique index over Email, GLOBAL (ADR-0061 D5.1): one identity per email across the
    // holding. Every anonymous identity read (login, lockout, reset, OTP, social link) resolves by email
    // ignoring the tenant, so a per-tenant scope would make login ambiguous the day a second operating
    // company opens. citext keeps it case-insensitive.
    [Fact]
    public void Email_HasExactlyOneGlobalUniqueIndex()
    {
        var user = GetUserEntityType();

        var emailIndexes = user.GetIndexes()
            .Where(ix => IsSingleColumnIndexOn(ix, nameof(User.Email)))
            .ToList();

        Assert.Single(emailIndexes);
        Assert.True(
            emailIndexes[0].IsUnique,
            "User email uniqueness must be the GLOBAL Email UNIQUE index (ADR-0061 D5.1): the anonymous "
            + "identity reads resolve by email ignoring the tenant, so the constraint must be global too.");

        // And there must be NO per-tenant composite unique index on Email left behind.
        Assert.DoesNotContain(
            user.GetIndexes(),
            ix => IsCompositeIndexOn(ix, nameof(User.TenantId), nameof(User.Email)));
    }

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

    // The nullable lookup columns are FILTERED/PARTIAL (WHERE "Col" IS NOT NULL).
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

    // The unique Email guarantee is real DB-level metadata, not an app-only pre-check.
    [Fact]
    public void Email_UniqueIndex_IsTheDbLevelGuarantee_NotJustTheAppPreCheck()
    {
        var user = GetUserEntityType();

        var emailUnique = user.GetIndexes()
            .Any(ix => IsSingleColumnIndexOn(ix, nameof(User.Email)) && ix.IsUnique);

        Assert.True(
            emailUnique,
            "A UNIQUE DB index over Email is the real guarantee — "
            + "the second same-email insert, in any tenant, must raise a unique violation. The "
            + "ExistsWithEmailIgnoringTenantAsync app pre-check stays as a fast-path UX message, but it is "
            + "NOT the constraint.");
    }

    // The four User-creating writers map this index's 23505 to a business error by NAME (ADR-0050 D2),
    // and AppServices cannot reference the entity configuration that decides that name. This assertion
    // is the only thing coupling the two: without it a rename turns every mapped duplicate-email
    // refusal back into a 500, silently.
    [Fact]
    public void Email_Index_Is_Unique_So_Two_Simultaneous_Registrations_Cannot_Both_Land()
    {
        var user = GetUserEntityType();

        var index = user.GetIndexes()
            .Single(ix => IsSingleColumnIndexOn(ix, nameof(User.Email)));

        Assert.True(index.IsUnique);
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
