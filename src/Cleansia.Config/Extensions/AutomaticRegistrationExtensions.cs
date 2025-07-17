using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Extensions;

public static class AutomaticRegistrationExtensions
{
    public static IServiceCollection RegisterFromAssemblies(this IServiceCollection services, Assembly[] assemblies, Func<Type, IEnumerable<Type>> interfaceSelector, bool addScoped = true)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        var types = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var type in types)
        {
            foreach (var @interface in interfaceSelector(type))
            {
                var nonGenericTypes = type.GetInterfaces().Where(x => !x.IsGenericType);
                foreach (var nonGenericType in nonGenericTypes)
                {
                    if (addScoped)
                    {
                        services.AddScoped(nonGenericType, type);
                    }
                    else
                    {
                        services.AddSingleton(@interface, type);
                    }
                }
            }
        }

        return services;
    }
}