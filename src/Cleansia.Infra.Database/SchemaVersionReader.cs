using Cleansia.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database;

/// <summary>
/// The latest migration applied to the database; the assembly's latest when none is recorded (a
/// database built by <c>EnsureCreated</c> carries no history).
/// </summary>
public sealed class SchemaVersionReader(CleansiaDbContext context) : ISchemaVersionReader
{
    public async Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational())
        {
            return null;
        }

        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        return applied.LastOrDefault() ?? context.Database.GetMigrations().LastOrDefault();
    }
}
