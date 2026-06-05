using Cleansia.Config.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Repositories;

public static class RepositoryExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserSessionProvider, UserSessionProvider>();
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<IRequestMetadataProvider, RequestMetadataProvider>();

        // ADR-0002 D3 (F3) — the durable dead-letter store consumed by the <queue>-poison consumers.
        // Its DB-backed impl lives in Cleansia.Infra.Database (DbContext + IDeadLetterRepository, the
        // latter auto-registered by the IRepository<,> scan below). Registered here alongside the other
        // queue/persistence services; resolvable from the Functions host (it calls AddCoreBindings).
        services.AddScoped<IDeadLetterStore, DeadLetterStore>();

        return services.RegisterFromAssemblies([AssemblyReference.Assembly], type => type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepository<,>)));
    }
}