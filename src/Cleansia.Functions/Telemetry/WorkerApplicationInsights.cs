using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Functions.Telemetry;

public static class WorkerApplicationInsights
{
    public static IServiceCollection AddWorkerApplicationInsights(this IServiceCollection services)
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddSingleton<ITelemetryInitializer, OutboundDependencyUrlRedaction>();
        return services;
    }
}
