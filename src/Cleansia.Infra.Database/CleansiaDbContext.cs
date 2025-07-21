using System.Security.Claims;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cleansia.Infra.Database;

public class CleansiaDbContext : DbContext, IUnitOfWork
{
    private readonly IUserSessionProvider userSessionProvider;

    public CleansiaDbContext()
    {
    }

    public CleansiaDbContext(IUserSessionProvider userSessionProvider)
    {
        this.userSessionProvider = userSessionProvider;
    }

    public CleansiaDbContext(DbContextOptions dbContextOptions)
        : base(dbContextOptions)
    {
    }

    public CleansiaDbContext(DbContextOptions dbContextOptions, IUserSessionProvider userSessionProvider)
        : base(dbContextOptions)
    {
        this.userSessionProvider = userSessionProvider;
    }

    public void Migrate()
    {
        // Database.Migrate();
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            var fullUserName = userSessionProvider.GetTypedUserClaim(ClaimTypes.Name)?.Value;
            var stateUser = string.IsNullOrWhiteSpace(fullUserName) ? "System" : fullUserName;
            var currentTime = DateTime.UtcNow;
            foreach (var entity in ChangeTracker.Entries<Auditable>())
            {
                if (entity.State == EntityState.Added)
                {
                    entity.Entity.Created(stateUser, currentTime);
                }
                else if (entity.State == EntityState.Modified)
                {
                    entity.Entity.Updated(stateUser, currentTime);
                }
            }
            return await SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            Rollback();
        }
        return 0;
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }

    public void Rollback()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            entry.State = EntityState.Unchanged;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

        modelBuilder.HasPostgresExtension("pg_trgm");
    }

    // Entities

    public virtual DbSet<Service> Services { get; set; }
    public virtual DbSet<Package> Packages { get; set; }
    public virtual DbSet<Currency> Currencies { get; set; }
    public virtual DbSet<Language> Languages { get; set; }
    public virtual DbSet<PackageService> PackageServices { get; set; }
    public virtual DbSet<Order> Orders { get; set; }

    // Views
}