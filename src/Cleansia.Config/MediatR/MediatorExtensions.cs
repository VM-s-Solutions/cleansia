using Cleansia.Core.AppServices;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.MediatR;

public static class MediatorExtensions
{
    public static IServiceCollection AddMediator(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly);
        });

        return services;
    }
}