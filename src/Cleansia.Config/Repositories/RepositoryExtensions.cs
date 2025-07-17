using Cleansia.Config.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Repositories;

public static class RepositoryExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserSessionProvider, UserSessionProvider>();

        return services.RegisterFromAssemblies([AssemblyReference.Assembly], type => type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepository<,>)));
    }
}