using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cleansia.Infra.Database;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> commands (migrations bundle, add migration, etc.)
/// when no running application host is available.
/// </summary>
public class DesignTimeCleansiaDbContextFactory : IDesignTimeDbContextFactory<CleansiaDbContext>
{
    public CleansiaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CleansiaDbContext>();
        // The connection string is only needed at runtime (efbundle --connection "...").
        // At design time we just need the provider configured so EF can discover migrations.
        optionsBuilder.UseNpgsql("Host=localhost;Database=cleansia_design_time");

        return new CleansiaDbContext(optionsBuilder.Options);
    }
}
