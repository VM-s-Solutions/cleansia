using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.SavedAddresses;

/// <summary>
/// Spins a real <see cref="CleansiaDbContext"/> over SQLite in-memory so the read filters and the
/// soft-delete audit stamping run against the actual EF pipeline. Covers: a deactivated saved address
/// is excluded from the user's list and from the default lookup (there is no global IsActive filter,
/// so each read must carry its own), and <see cref="ISavedAddressRepository.Deactivate"/> stamps
/// DeactivatedBy/DeactivatedOn from the caller while leaving the row in place.
/// </summary>
public sealed class SavedAddressRepositorySoftDeleteTests : IDisposable
{
    private const string UserId = "user-1";
    private const string Actor = "user-1";

    private readonly SqliteConnection _connection;

    public SavedAddressRepositorySoftDeleteTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private static IUserSessionProvider NewSession() =>
        new TestUserSessionProvider(Actor, "user@cleansia.test");

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            NewSession(),
            new NullTenantProvider());
    }

    private async Task<(string ActiveId, string DeactivatedId)> SeedOneActiveOneDeactivatedAsync(bool deactivatedIsDefault)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var language = Language.Create("en", "English");
        var user = User.CreateWithPassword("owner@cleansia.test", "Passw0rd!", "Owner", "User");
        user.Id = UserId;
        var country = Country.Create("Czechia", "CZE");
        country.Id = "country-1";
        var activeAddress = NewAddress("addr-active", country.Id);
        var deactivatedAddress = NewAddress("addr-deactivated", country.Id);
        ctx.Add(language);
        ctx.Add(user);
        ctx.Add(country);
        ctx.Add(activeAddress);
        ctx.Add(deactivatedAddress);

        var active = SavedAddress.Create(UserId, activeAddress.Id, "Work", isDefault: false);
        active.Id = "saved-active";
        var deactivated = SavedAddress.Create(UserId, deactivatedAddress.Id, "Home", isDefault: deactivatedIsDefault);
        deactivated.Id = "saved-deactivated";
        ctx.Add(active);
        ctx.Add(deactivated);
        await ctx.CommitAsync(CancellationToken.None);

        var repo = new SavedAddressRepository(ctx, NewSession());
        repo.Deactivate(deactivated);
        await ctx.CommitAsync(CancellationToken.None);

        return (active.Id, deactivated.Id);
    }

    private static Address NewAddress(string id, string countryId)
    {
        var address = Address.Create("Main Street 1", "Prague", "11000", countryId);
        address.Id = id;
        return address;
    }

    [Fact]
    public async Task GetByUserAsync_Excludes_Deactivated()
    {
        var (activeId, deactivatedId) = await SeedOneActiveOneDeactivatedAsync(deactivatedIsDefault: false);

        await using var ctx = NewContext();
        var result = await new SavedAddressRepository(ctx, NewSession()).GetByUserAsync(UserId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(activeId, result[0].Id);
        Assert.DoesNotContain(result, s => s.Id == deactivatedId);
    }

    [Fact]
    public async Task GetDefaultForUserAsync_Never_Returns_A_Deactivated_Default()
    {
        await SeedOneActiveOneDeactivatedAsync(deactivatedIsDefault: true);

        await using var ctx = NewContext();
        var result = await new SavedAddressRepository(ctx, NewSession()).GetDefaultForUserAsync(UserId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Deactivate_Sets_IsActive_False_And_Stamps_Audit_While_Row_Survives()
    {
        var (_, deactivatedId) = await SeedOneActiveOneDeactivatedAsync(deactivatedIsDefault: false);

        await using var ctx = NewContext();
        var row = await ctx.Set<SavedAddress>().IgnoreQueryFilters().FirstAsync(s => s.Id == deactivatedId);

        Assert.False(row.IsActive);
        Assert.Equal(Actor, row.DeactivatedBy);
        Assert.NotNull(row.DeactivatedOn);
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
