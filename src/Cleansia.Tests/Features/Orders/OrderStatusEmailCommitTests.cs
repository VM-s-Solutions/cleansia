using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cleansia.Tests.Features.Orders;

public sealed class OrderStatusEmailCommitTests : IDisposable
{
    private const string OrderId = "order-status-email-commit";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Mock<IEmailService> _email = new();
    public OrderStatusEmailCommitTests()
    {
        _connection.Open();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=OFF";
        cmd.ExecuteNonQuery();
    }
    public void Dispose() => _connection.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Refused_Status_Commit_Sends_Nothing_And_A_Retry_Persists_One_Email(bool starting)
    {
        await using (var seed = Context())
        {
            await seed.Database.EnsureCreatedAsync();
            var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId,
                starting ? OrderStatus.Confirmed : OrderStatus.InProgress, maxEmployees: 1);
            var currency = Currency.Create("CZK", "Kč", "Czech koruna");
            currency.Id = order.CurrencyId;
            seed.Currencies.Add(currency);
            var country = Country.Create("Czechia", "CZ", "CZ", true);
            country.Id = "cz";
            seed.Countries.Add(country);
            seed.Orders.Add(order);
            await seed.CommitAsync(CancellationToken.None);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(starting, new RefuseSave()));
        _email.Verify(e => e.SendOrderStatusUpdateEmailAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<decimal?>(), It.IsAny<string?>()), Times.Never);
        await using (var afterFailure = Context())
        {
            Assert.Empty(await afterFailure.OutboxMessages.IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await afterFailure.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
        }
        await RunAsync(starting);
        await using var read = Context();
        Assert.Single(await read.OutboxMessages.IgnoreQueryFilters().Where(m => m.QueueName == QueueNames.SendEmail).ToListAsync());
        Assert.Empty(await read.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
    }

    private async Task RunAsync(bool starting, params IInterceptor[] interceptors)
    {
        await using var ctx = Context(interceptors);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOrderRepository>(new OrderRepository(ctx));
        services.AddSingleton(_email.Object);
        services.AddSingleton(new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)));
        services.AddSingleton(Mock.Of<INotificationProducer>());
        services.AddSingleton(Mock.Of<ILiveActivityProducer>());
        services.AddSingleton(Mock.Of<ILoyaltyService>());
        services.AddSingleton(Mock.Of<IReferralService>());
        services.AddSingleton<IPendingDispatch>(new OutboxPendingDispatch(ctx));
        await using var provider = services.BuildServiceProvider();
        if (starting)
        {
            var handler = ActivatorUtilities.CreateInstance<StartOrder.Handler>(provider);
            var command = new StartOrder.Command(OrderId);
            await new UnitOfWorkPipelineBehavior<StartOrder.Command, BusinessResult<StartOrder.Response>>(ctx)
                .Handle(command, ct => handler.Handle(command, ct), CancellationToken.None);
        }
        else
        {
            var handler = ActivatorUtilities.CreateInstance<CompleteOrder.Handler>(provider);
            var command = new CompleteOrder.Command(OrderId, 60);
            await new UnitOfWorkPipelineBehavior<CompleteOrder.Command, BusinessResult<CompleteOrder.Response>>(ctx)
                .Handle(command, ct => handler.Handle(command, ct), CancellationToken.None);
        }
    }

    private CleansiaDbContext Context(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).AddInterceptors(interceptors).Options,
        new TestUserSessionProvider("cleaner", "cleaner@cleansia.test"), new FixedTenantProvider(TestTenants.Default));
    private sealed class RefuseSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default) => throw new InvalidOperationException("Refused status commit");
    }
    private sealed class FixedTenantProvider(string tenant) : ITenantProvider
    {
        public string? GetCurrentTenantId() => tenant;
        public void SetTenantOverride(string value) { }
        public void ClearTenantOverride() { }
    }
}
