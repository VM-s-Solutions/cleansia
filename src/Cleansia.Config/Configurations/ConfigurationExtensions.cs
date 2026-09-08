using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Configurations;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddConfigurationBindings(this IServiceCollection services)
    {
        services.AddSingleton<IStripeConfig, StripeConfig>();
        services.AddSingleton<ISendGridConfig, SendGridConfig>();
        services.AddSingleton<IJwtSettings, JwtSettingsConfig>();
        services.AddSingleton<IGoogleConfig, GoogleConfig>();
        services.AddSingleton<IAppleConfig, AppleConfig>();
        services.AddSingleton<IMapboxConfig, MapboxConfig>();
        services.AddSingleton<IFcmConfig, FcmConfig>();
        services.AddSingleton<IApnsConfig, ApnsConfig>();
        // ADR-0002 D3.4 — reconciliation-sweep tunables (threshold default 15
        // min, batch 50), bound from the "FiscalReconciliation" section.
        services.AddSingleton<IFiscalReconciliationConfig, FiscalReconciliationConfig>();
        // Outbox drainer tunables (batch size / retry ceiling / backoff base),
        // bound from the "OutboxDrainer" section.
        services.AddSingleton<IOutboxDrainerConfig, OutboxDrainerConfig>();
        // Outbox retention-prune tunables (toggle + retention windows + batch size),
        // bound from the "OutboxRetention" section.
        services.AddSingleton<IOutboxRetentionConfig, OutboxRetentionConfig>();
        // The GDPR retention sweep's master switch, bound from the "DataRetention" section. Defaults ON
        // when the section is absent — it replaced a database row that no deployed database ever had,
        // which resolved to "off" and silenced the sweep everywhere but a developer's machine.
        services.AddSingleton<IDataRetentionConfig, DataRetentionConfig>();
        // The nightly pay-period job's master switch, bound from the "PayPeriodClosing" section. That
        // job generates and emails an invoice per employee, and had no switch at all before T-0689.
        services.AddSingleton<IPayPeriodClosingConfig, PayPeriodClosingConfig>();

        return services;
    }
}