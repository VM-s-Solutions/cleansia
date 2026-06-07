using System.Diagnostics.Metrics;

namespace Cleansia.Core.Clients.Abstractions;

/// <summary>
/// The owner-alertable counter every integration boundary increments when an outbound call fails, so
/// a spike in <see cref="IntegrationFailureClass.Permanent"/>/<see cref="IntegrationFailureClass.AuthConfig"/>
/// (a rotated key, a changed provider contract) is observable before it becomes an incident. Tagged by
/// provider + failure class only — never by recipient/customer/key (S6, PII-free).
/// </summary>
public static class IntegrationFailureMetrics
{
    public const string MeterName = "Cleansia.Integration";
    public const string FailureCounterName = "cleansia.integration.failures";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Failures =
        Meter.CreateCounter<long>(FailureCounterName, unit: "{failure}",
            description: "Outbound integration failures, dimensioned by provider + failure class only (S6).");

    /// <summary>Records one classified failure for <paramref name="provider"/> (e.g. "SendGrid").</summary>
    public static void Record(string provider, IntegrationFailureClass failureClass) =>
        Failures.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("class", failureClass.ToString()));
}
