using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Cleansia.Config.Database;

public static class DbContextBindingExtensions
{
    public static IServiceCollection AddDbContextBindings(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        services.AddSingleton<IDatabaseConnectionString, DatabaseConnectionString>();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Ensure citext extension exists before Npgsql caches the type catalog
        TryEnsureCitextExtension(connectionString!);

        services.AddDbContext<CleansiaDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetService<CleansiaDbContext>()!);

        return services;
    }

    private static void TryEnsureCitextExtension(string connectionString)
    {
        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS citext;";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Could not ensure citext extension: {ex.Message}. " +
                "Make sure the extension is allow-listed in Azure Database for PostgreSQL " +
                "(Server parameters > azure.extensions > add CITEXT).");
        }
    }
}