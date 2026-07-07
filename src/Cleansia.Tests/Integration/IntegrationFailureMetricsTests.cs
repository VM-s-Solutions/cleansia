using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Cleansia.Core.Clients.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// The owner-alertable counter: a <c>Permanent</c>/<c>AuthConfig</c> spike at any integration
/// boundary must be observable. Each boundary increments <see cref="IntegrationFailureMetrics"/>
/// dimensioned by provider + failure class only (PII-free, S6) — never by recipient/customer/key.
///
/// Hermeticity: the meter is process-global, so boundary tests running in parallel collections
/// (SendGrid/Mapbox/Stripe/FCM) emit onto it concurrently. The listener therefore keeps only
/// measurements for this test's unique provider names and sinks them into a thread-safe queue —
/// a plain list would be mutated by a foreign-thread callback mid-assertion. Tests that must
/// assert on REAL provider names cannot filter their way to hermeticity; they join
/// <see cref="IntegrationFailureMeterCollection"/> instead.
/// </summary>
public class IntegrationFailureMetricsTests
{
    [Fact]
    public void Recording_A_Failure_Increments_The_Counter_Tagged_By_Provider_And_Class()
    {
        var provider = UniqueProvider();
        var measurements = new ConcurrentQueue<(long Value, string? Provider, string? Class)>();
        using var listener = ListenForFailures(measurements, provider);

        IntegrationFailureMetrics.Record(provider, IntegrationFailureClass.AuthConfig);

        var match = Assert.Single(measurements.ToArray(), m => m.Provider == provider);
        Assert.Equal(1, match.Value);
        Assert.Equal(nameof(IntegrationFailureClass.AuthConfig), match.Class);
    }

    [Fact]
    public void Each_Boundary_Records_Independently()
    {
        var first = UniqueProvider();
        var second = UniqueProvider();
        var measurements = new ConcurrentQueue<(long Value, string? Provider, string? Class)>();
        using var listener = ListenForFailures(measurements, first, second);

        IntegrationFailureMetrics.Record(first, IntegrationFailureClass.AuthConfig);
        IntegrationFailureMetrics.Record(second, IntegrationFailureClass.Permanent);

        var snapshot = measurements.ToArray();
        Assert.Contains(snapshot, m => m.Provider == first && m.Class == nameof(IntegrationFailureClass.AuthConfig));
        Assert.Contains(snapshot, m => m.Provider == second && m.Class == nameof(IntegrationFailureClass.Permanent));
    }

    private static string UniqueProvider() => $"Test-{Guid.NewGuid():N}";

    private static MeterListener ListenForFailures(
        ConcurrentQueue<(long, string?, string?)> sink,
        params string[] ownProviders)
    {
        var accepted = new HashSet<string>(ownProviders, StringComparer.Ordinal);
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == IntegrationFailureMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name != IntegrationFailureMetrics.FailureCounterName)
            {
                return;
            }

            string? provider = null;
            string? failureClass = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "provider") provider = tag.Value as string;
                if (tag.Key == "class") failureClass = tag.Value as string;
            }

            if (provider is null || !accepted.Contains(provider))
            {
                return;
            }

            sink.Enqueue((value, provider, failureClass));
        });
        listener.Start();
        return listener;
    }
}
