using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Database;
using Cleansia.Infra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Services;

public static class ServiceExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IFiscalRetryService, FiscalRetryService>();
        services.AddScoped<IAppConfigurationProvider, AppConfigurationProvider>();
        services.AddScoped<ITaxIdValidator, TaxIdValidator>();
        services.AddScoped<IVatCalculator, VatCalculator>();
        services.AddScoped<ICurrencyResolutionService, CurrencyResolutionService>();
        services.AddScoped<IOrderPricingCalculator, OrderPricingCalculator>();
        services.AddScoped<IOrderFactory, OrderFactory>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<IPromoCodeService, PromoCodeService>();
        services.AddScoped<IReferralService, ReferralService>();
        services.AddScoped<IStripeSubscriptionWebhookHandler, StripeSubscriptionWebhookHandler>();
        services.AddScoped<ICancellationPolicyResolver, CancellationPolicyResolver>();
        services.AddScoped<IOrderAccessService, OrderAccessService>();
        services.AddScoped<IAddressGeocoder, AddressGeocoder>();
        services.AddScoped<IGdprDeletionService, GdprDeletionService>();
        services.AddScoped<IGdprExportService, GdprExportService>();
        services.AddInfrastructureServices();

        return services;
    }
}