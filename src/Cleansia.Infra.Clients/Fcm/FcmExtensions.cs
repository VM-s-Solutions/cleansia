using Cleansia.Core.Clients.Abstractions.Fcm;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Infra.Clients.Fcm;

public static class FcmExtensions
{
    public static IServiceCollection AddFcm(this IServiceCollection services)
    {
        // Singleton: FirebaseApp itself is process-singleton, and FcmPushDispatcher
        // caches the messaging client. No per-request state to scope around.
        services.AddSingleton<IPushDispatcher, FcmPushDispatcher>();
        return services;
    }
}
