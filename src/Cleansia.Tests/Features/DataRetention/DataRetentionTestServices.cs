using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Features.DataRetention;

internal static class DataRetentionTestServices
{
    public static ServiceProvider Create(
        Func<ITenantProvider, IArchiveWriteGate, CleansiaDbContext> contextFactory,
        IDataRetentionConfig config,
        IBlobContainerClientFactory blobs,
        Func<CleansiaDbContext, IAppConfigurationProvider>? settings = null,
        ILogger<DataRetentionBackgroundService>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantProvider, RetentionTenantProvider>();
        services.AddScoped<IArchiveWriteGate, ArchiveWriteGate>();
        services.AddScoped(sp => contextFactory(sp.GetRequiredService<ITenantProvider>(), sp.GetRequiredService<IArchiveWriteGate>()));
        services.AddSingleton<IUserSessionProvider>(new TestUserSessionProvider("system", "system@cleansia.test"));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IGdprRequestRepository, GdprRequestRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUserConsentRepository, UserConsentRepository>();
        services.AddScoped<IEmployeeDocumentRepository, EmployeeDocumentRepository>();
        services.AddScoped<IUserNotificationRepository, UserNotificationRepository>();
        services.AddScoped<ICustomerActionAuditRepository, CustomerActionAuditRepository>();
        services.AddScoped<IDisputeRepository, DisputeRepository>();
        services.AddScoped<IWorkContractAcceptanceRepository, WorkContractAcceptanceRepository>();
        services.AddScoped<ICleanerLegalDocumentAcceptanceRepository, CleanerLegalDocumentAcceptanceRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IOrderPhotoRepository, OrderPhotoRepository>();
        services.AddScoped<IOrderReceiptRepository, OrderReceiptRepository>();
        services.AddScoped<IAdminActionAuditRepository, AdminActionAuditRepository>();
        services.AddScoped<IEmployeeActionAuditRepository, EmployeeActionAuditRepository>();
        services.AddScoped<IGuestOrderAccessTokenRepository, GuestOrderAccessTokenRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IAppConfigurationProvider>(sp => settings?.Invoke(sp.GetRequiredService<CleansiaDbContext>())
            ?? new AppConfigurationProvider(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddSingleton(config);
        services.AddSingleton(blobs);
        services.AddSingleton(logger ?? NullLogger<DataRetentionBackgroundService>.Instance);
        services.AddScoped<DataRetentionBackgroundService>();
        services.AddScoped<IDataRetentionBackgroundService>(sp => sp.GetRequiredService<DataRetentionBackgroundService>());
        return services.BuildServiceProvider();
    }

    private sealed class RetentionTenantProvider : ITenantProvider
    {
        private string? tenantId;
        public string? GetCurrentTenantId() => tenantId;
        public void SetTenantOverride(string value) => tenantId = value;
        public void ClearTenantOverride() => tenantId = null;
    }
}
