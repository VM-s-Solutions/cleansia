using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Cleansia.Config.Database;

public static class DbContextBindingExtensions
{
    public static IServiceCollection AddDbContextBindings(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        services.AddSingleton<IDatabaseConnectionString, DatabaseConnectionString>();

        var connectionString = configuration.GetConnectionString("ConnectionString");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddDbContext<CleansiaDbContext>(options => options.UseNpgsql(dataSource));
        services.AddScoped<IUnitOfWork>(provider => provider.GetService<CleansiaDbContext>()!);

        return services;
    }
}