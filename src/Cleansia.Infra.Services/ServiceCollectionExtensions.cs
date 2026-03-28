using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Infra.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<ITemplateEngine, HandlebarsTemplateEngine>();

        // PDF layout builders
        services.AddSingleton<IReceiptLayoutBuilder, DefaultReceiptLayoutBuilder>();
        services.AddSingleton<IInvoiceLayoutBuilder, DefaultInvoiceLayoutBuilder>();
        services.AddSingleton<LayoutBuilderFactory>();
        services.AddScoped<IPdfService, QuestPdfService>();

        return services;
    }
}
