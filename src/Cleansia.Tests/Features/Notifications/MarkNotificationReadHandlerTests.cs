using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Notifications;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using FluentValidation.TestHelper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Notifications;

/// <summary>
/// FD-AC7 — single mark-read: owner-only (a foreign user's — or the wrong host audience's — attempt
/// returns NotFound and leaves the row unchanged, never confirming existence), and idempotent (the
/// first ReadOn timestamp wins across repeat calls).
/// </summary>
public sealed class MarkNotificationReadHandlerTests : IDisposable
{
    private const string OwnerUserId = "user-read-owner";
    private const string OtherUserId = "user-read-other";

    private readonly SqliteConnection _connection;

    public MarkNotificationReadHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));

    private async Task<string> SeedRowAsync(string eventKey)
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        var row = UserNotification.Create(OwnerUserId, eventKey, "{}", null);
        await using (var seed = NewContext())
        {
            seed.Add(row);
            await seed.CommitAsync(CancellationToken.None);
        }

        return row.Id;
    }

    private async Task<Cleansia.Infra.Common.Validations.BusinessResult<MarkNotificationRead.Response>> HandleAsync(
        MarkNotificationRead.Command command, string callerUserId)
    {
        await using var ctx = NewContext();
        var handler = new MarkNotificationRead.Handler(
            new UserNotificationRepository(ctx),
            new TestUserSessionProvider(callerUserId, "caller@cleansia.test"));

        var result = await handler.Handle(command, CancellationToken.None);
        // The UnitOfWork pipeline commits successful commands; mirror it so persistence is observable.
        if (result.IsSuccess)
        {
            await ctx.CommitAsync(CancellationToken.None);
        }

        return result;
    }

    private async Task<UserNotification> ReadRowAsync(string id)
    {
        await using var ctx = NewContext();
        return await ctx.Set<UserNotification>().IgnoreQueryFilters().SingleAsync(n => n.Id == id);
    }

    [Fact]
    public async Task Owner_Marks_Read_And_A_Second_Call_Keeps_The_First_Timestamp()
    {
        var id = await SeedRowAsync(NotificationEventCatalog.OrderCompleted);

        var first = await HandleAsync(new MarkNotificationRead.Command(id), OwnerUserId);
        Assert.True(first.IsSuccess);
        var firstReadOn = first.Value!.ReadOn;
        Assert.Equal(firstReadOn, (await ReadRowAsync(id)).ReadOn);

        var second = await HandleAsync(new MarkNotificationRead.Command(id), OwnerUserId);
        Assert.True(second.IsSuccess);
        Assert.Equal(firstReadOn, second.Value!.ReadOn);
        Assert.Equal(firstReadOn, (await ReadRowAsync(id)).ReadOn);
    }

    [Fact]
    public async Task Foreign_User_Gets_NotFound_And_The_Row_Stays_Unread()
    {
        var id = await SeedRowAsync(NotificationEventCatalog.OrderCompleted);

        var result = await HandleAsync(new MarkNotificationRead.Command(id), OtherUserId);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NotFound, result.Error!.Message);
        Assert.Null((await ReadRowAsync(id)).ReadOn);
    }

    [Fact]
    public async Task Wrong_Host_Audience_Gets_NotFound_Even_For_The_Owner()
    {
        // A dual-role user's partner digest row must not be consumable from the customer host.
        var id = await SeedRowAsync(NotificationEventCatalog.NewJobsAvailable);

        var result = await HandleAsync(
            new MarkNotificationRead.Command(id, NotificationFeedAudience.Customer), OwnerUserId);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NotFound, result.Error!.Message);
        Assert.Null((await ReadRowAsync(id)).ReadOn);
    }

    [Fact]
    public async Task Missing_Row_Returns_NotFound()
    {
        await SeedRowAsync(NotificationEventCatalog.OrderCompleted);

        var result = await HandleAsync(new MarkNotificationRead.Command("no-such-row"), OwnerUserId);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NotFound, result.Error!.Message);
    }

    [Fact]
    public void Validator_Rejects_An_Empty_Id()
    {
        new MarkNotificationRead.Validator()
            .TestValidate(new MarkNotificationRead.Command(string.Empty))
            .ShouldHaveValidationErrorFor(c => c.Id)
            .WithErrorMessage(BusinessErrorMessage.Required);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
