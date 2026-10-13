using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Services;

internal static class NewJobsDigestTestServices
{
    public static ServiceProvider Create(
        Func<CleansiaDbContext> contextFactory,
        INotificationProducer producer,
        Func<CleansiaDbContext, IOrderRepository>? orders = null,
        ILogger<NewJobsDigestService>? logger = null,
        IUnitOfWork? unitOfWork = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => contextFactory());
        services.AddScoped<ITenantProvider, DigestTenantProvider>();
        services.AddScoped<IEmployeeRepository>(sp => new EmployeeRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<IOrderRepository>(sp => orders?.Invoke(sp.GetRequiredService<CleansiaDbContext>())
            ?? new OrderRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<IUserNotificationPreferencesRepository>(sp => new UserNotificationPreferencesRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<IUnitOfWork>(sp => unitOfWork ?? sp.GetRequiredService<CleansiaDbContext>());
        services.AddSingleton(producer);
        services.AddSingleton(logger ?? NullLogger<NewJobsDigestService>.Instance);
        services.AddScoped<NewJobsDigestService>();
        services.AddScoped<INewJobsDigestService>(sp => sp.GetRequiredService<NewJobsDigestService>());
        return services.BuildServiceProvider();
    }

    private sealed class DigestTenantProvider : ITenantProvider
    {
        private string? _tenantId = TestTenants.Default;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
