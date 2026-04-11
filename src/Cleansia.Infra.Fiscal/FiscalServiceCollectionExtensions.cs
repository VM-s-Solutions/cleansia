using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Infra.Fiscal.Countries.Czechia;
using Cleansia.Infra.Fiscal.NoOp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Infra.Fiscal;

/// <summary>
/// Extension methods for registering fiscal services in DI.
/// Call <see cref="AddFiscalServices"/> from each web project's startup.
/// </summary>
public static class FiscalServiceCollectionExtensions
{
    /// <summary>
    /// Registers the fiscal service resolver, the no-op fallback, and all
    /// country-specific services whose <c>Enabled</c> flag is set in configuration.
    /// </summary>
    public static IServiceCollection AddFiscalServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Always register the no-op fallback and the resolver.
        services.AddSingleton<NoOpFiscalService>();
        services.AddScoped<IFiscalServiceResolver, FiscalServiceResolver>();

        RegisterCzechEet2(services, configuration);

        // Future countries follow the same pattern:
        //   RegisterSlovakEkasa(services, configuration);
        //   RegisterGermanFiskalyTss(services, configuration);
        //   RegisterAustrianFiskalyRksv(services, configuration);
        //   RegisterSpanishVeriFactu(services, configuration);
        //   RegisterItalianPosRt(services, configuration);
        //   RegisterFrenchNf525(services, configuration);
        //   RegisterSwedishSkvfs(services, configuration);

        return services;
    }

    private static void RegisterCzechEet2(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CzechEet2Options>(
            configuration.GetSection(CzechEet2Options.SectionName));

        var enabled = configuration.GetValue<bool>($"{CzechEet2Options.SectionName}:Enabled");
        if (!enabled)
        {
            return;
        }

        services.AddHttpClient<CzechEet2FiscalService>()
            .AddStandardResilienceHandler();
        services.AddScoped<IFiscalService, CzechEet2FiscalService>();
    }
}
