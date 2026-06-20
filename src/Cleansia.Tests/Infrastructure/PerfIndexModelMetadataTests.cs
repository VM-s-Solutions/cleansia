using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// Asserts the EF model metadata for the supporting indexes this PERF sweep adds (AC2/AC3). The
/// verifiable unit for an index change is the model's <see cref="IEntityType.GetIndexes"/> — not a
/// runtime query plan — so these pin that each index exists on the right column tuple. No query
/// result changes; behaviour is unchanged.
/// </summary>
public sealed class PerfIndexModelMetadataTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PerfIndexModelMetadataTests()
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
        return new CleansiaDbContext(options, new TestUserSessionProvider("system", "system@cleansia.test"), new NullTenantProvider());
    }

    private static bool HasIndexOn<TEntity>(CleansiaDbContext ctx, params string[] columns)
    {
        var entityType = ctx.Model.FindEntityType(typeof(TEntity))!;
        return entityType.GetIndexes().Any(ix =>
            ix.Properties.Select(p => p.Name).SequenceEqual(columns));
    }

    private static bool HasPartialIndexOn<TEntity>(CleansiaDbContext ctx, string filter, params string[] columns)
    {
        var entityType = ctx.Model.FindEntityType(typeof(TEntity))!;
        return entityType.GetIndexes().Any(ix =>
            ix.Properties.Select(p => p.Name).SequenceEqual(columns)
            && ix.GetFilter() == filter);
    }

    [Fact]
    public void Addresses_HasCompositeDedupIndex_CountryId_ZipCode_City_Street()
    {
        using var ctx = NewContext();
        Assert.True(HasIndexOn<Address>(ctx,
            nameof(Address.CountryId), nameof(Address.ZipCode), nameof(Address.City), nameof(Address.Street)));
    }

    [Fact]
    public void UserMembership_HasStatusCurrentPeriodEndIndex_ForLifecycleSweep()
    {
        using var ctx = NewContext();
        Assert.True(HasIndexOn<UserMembership>(ctx,
            nameof(UserMembership.Status), nameof(UserMembership.CurrentPeriodEnd)));
    }

    [Fact]
    public void UserMembership_HasRenewalReminderPartialIndex_ForRenewalArm()
    {
        using var ctx = NewContext();
        Assert.True(HasPartialIndexOn<UserMembership>(ctx,
            "\"RenewalReminderSentAt\" IS NULL",
            nameof(UserMembership.Status), nameof(UserMembership.CurrentPeriodEnd)));
    }

    [Fact]
    public void UserMembership_HasCancellationReminderPartialIndex_ForCancellationArm()
    {
        using var ctx = NewContext();
        Assert.True(HasPartialIndexOn<UserMembership>(ctx,
            "\"CancelledAt\" IS NOT NULL AND \"CancellationReminderSentAt\" IS NULL",
            nameof(UserMembership.Status), nameof(UserMembership.CurrentPeriodEnd)));
    }

    [Fact]
    public void GdprRequest_HasCreatedOnIndex_ForAdminSort()
    {
        using var ctx = NewContext();
        Assert.True(HasIndexOn<GdprRequest>(ctx, nameof(GdprRequest.CreatedOn)));
    }

    [Fact]
    public void Devices_HasIsActiveLastActiveAtIndex_ForStaleSweep()
    {
        using var ctx = NewContext();
        Assert.True(HasIndexOn<Device>(ctx, nameof(Device.IsActive), nameof(Device.LastActiveAt)));
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
