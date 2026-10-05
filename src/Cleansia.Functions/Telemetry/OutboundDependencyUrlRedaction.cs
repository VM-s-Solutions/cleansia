using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Cleansia.Functions.Telemetry;

/// <summary>
/// An outbound dependency keeps the host and never the path, which can hold a push token: the classic
/// collector's counterpart of the span redaction in the service defaults. An initializer rather than a
/// processor, so Live Metrics, which reads the processor chain, never sees the path either.
/// </summary>
public sealed class OutboundDependencyUrlRedaction : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is not DependencyTelemetry dependency
            || dependency.Type?.StartsWith("Http", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        dependency.Data = Uri.TryCreate(dependency.Data, UriKind.Absolute, out var uri)
            ? $"{uri.Scheme}://{uri.Authority}"
            : string.Empty;

        var method = dependency.Name?.IndexOf(' ') ?? -1;
        dependency.Name = method > 0 ? $"{dependency.Name![..method]} /" : "/";
    }
}
