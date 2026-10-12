using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// Runs <see cref="AutoCancelStaleRecurringOrders"/> over a SQLite connection the way the Functions host runs it:
/// the sweep in a scope of its own, and each occurrence's unit in another, each inside the real unit-of-work
/// pipeline, with a tenant provider per scope that starts empty as a timer's does. Only the mediator is a stand-in.
/// </summary>
internal static class RecurringAutoCancelSweep
{
    public static async Task<AutoCancelStaleRecurringOrders.Response> RunAsync(
        SqliteConnection connection,
        IInterceptor[]? interceptors = null,
        Func<INotificationProducer, INotificationProducer>? decorateNotifier = null)
    {
        await using var provider = BuildProvider(connection, interceptors ?? [], decorateNotifier ?? (inner => inner));
        await using var sweepScope = provider.CreateAsyncScope();
        var command = new AutoCancelStaleRecurringOrders.Command();
        var handler = new AutoCancelStaleRecurringOrders.Handler(
            sweepScope.ServiceProvider.GetRequiredService<IOrderRepository>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AutoCancelStaleRecurringOrders.Handler>.Instance);

        var result = await InUnitOfWork(sweepScope.ServiceProvider, command, handler.Handle, CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    /// <summary>Sends one occurrence's unit in a scope of its own, as the sweep does.</summary>
    public static async Task<AutoCancelRecurringOccurrence.Response> SendOccurrenceAsync(
        SqliteConnection connection, AutoCancelRecurringOccurrence.Command command)
    {
        await using var provider = BuildProvider(connection, [], inner => inner);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static ServiceProvider BuildProvider(
        SqliteConnection connection,
        IInterceptor[] interceptors,
        Func<INotificationProducer, INotificationProducer> decorateNotifier)
    {
        var session = new TestUserSessionProvider("system", "system@cleansia.test");
        var services = new ServiceCollection();

        services.AddScoped<ITenantProvider>(_ => new ScopeTenantProvider());
        services.AddScoped(sp => new CleansiaDbContext(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(connection).AddInterceptors(interceptors).Options,
            session,
            sp.GetRequiredService<ITenantProvider>()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CleansiaDbContext>());
        services.AddScoped<IOrderRepository>(sp => new OrderRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<ICreditAccountRepository>(
            sp => new CreditAccountRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<INotificationProducer>(sp =>
        {
            var ctx = sp.GetRequiredService<CleansiaDbContext>();
            return decorateNotifier(new NotificationProducer(
                new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx),
                NullLogger<NotificationProducer>.Instance));
        });
        services.AddScoped<AutoCancelRecurringOccurrence.Handler>();
        services.AddScoped<IMediator>(sp =>
        {
            var mediator = new Mock<IMediator>();
            mediator
                .Setup(m => m.Send(It.IsAny<AutoCancelRecurringOccurrence.Command>(), It.IsAny<CancellationToken>()))
                .Returns((AutoCancelRecurringOccurrence.Command c, CancellationToken ct) => InUnitOfWork(
                    sp, c, sp.GetRequiredService<AutoCancelRecurringOccurrence.Handler>().Handle, ct));
            return mediator.Object;
        });

        return services.BuildServiceProvider();
    }

    private static Task<BusinessResult<TResponse>> InUnitOfWork<TCommand, TResponse>(
        IServiceProvider scope,
        TCommand command,
        Func<TCommand, CancellationToken, Task<BusinessResult<TResponse>>> handle,
        CancellationToken cancellationToken)
        where TCommand : notnull =>
        new UnitOfWorkPipelineBehavior<TCommand, BusinessResult<TResponse>>(scope.GetRequiredService<IUnitOfWork>())
            .Handle(command, ct => handle(command, ct), cancellationToken);

    private sealed class ScopeTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
