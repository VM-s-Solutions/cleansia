using Cleansia.Config.Repositories;
using Cleansia.Config.Validation;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Azure.Storage.Queues;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// The backing swap and the in-Functions host shape, at the wiring level. The durable backing replaces
/// the in-memory one for every host that calls AddCoreBindings (APIs and the Functions worker), and the
/// post-commit dispatch behavior stays registered (so an in-Function command still writes a durable
/// row). The single drainer host is wired in Cleansia.Functions/Program.cs only — it is not a
/// per-instance dispatch behavior, which this test guards by asserting the dispatch behavior is
/// unchanged.
/// </summary>
public sealed class OutboxWiringTests
{
    [Fact]
    public void Repositories_Register_The_Durable_Outbox_Backing_For_PendingDispatch()
    {
        var services = new ServiceCollection().AddRepositories();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPendingDispatch));
        Assert.Equal(typeof(OutboxPendingDispatch), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void The_InMemory_Backing_Is_No_Longer_Registered()
    {
        var services = new ServiceCollection().AddRepositories();

        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(InMemoryPendingDispatch));
    }

    [Fact]
    public void The_PostCommit_Dispatch_Behavior_Stays_Registered_So_In_Function_Side_Effects_Are_Durable()
    {
        var services = new ServiceCollection().AddValidators();

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IPipelineBehavior<,>)
            && d.ImplementationType == typeof(PostCommitDispatchBehavior<,>));
    }
}
