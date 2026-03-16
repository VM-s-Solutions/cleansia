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

        // Ensure citext extension exists before Npgsql caches the type catalog
        EnsureCitextExtension(connectionString!);

        services.AddDbContext<CleansiaDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetService<CleansiaDbContext>()!);

        return services;
    }

    private static void EnsureCitextExtension(string connectionString)
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS citext;";
        cmd.ExecuteNonQuery();
    }
}