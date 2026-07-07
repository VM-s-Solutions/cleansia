using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Cleansia.Core.Clients.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// Captures <see cref="IntegrationFailureMetrics.FailureCounterName"/> measurements for boundary
/// tests that assert on REAL provider names. The meter is process-global and callbacks arrive on
/// whichever thread recorded the failure, so the sink is a <see cref="ConcurrentQueue{T}"/> — a
/// plain list would be mutated by a foreign-thread callback mid-assertion. Thread safety alone does
/// not make a real-provider assertion hermetic: every class using this capture must also join
/// <see cref="IntegrationFailureMeterCollection"/>.
/// </summary>
internal static class FailureMetricsCapture
{
    public static ConcurrentQueue<(string? Provider, string? Class)> Start(out MeterListener listener)
    {
        var sink = new ConcurrentQueue<(string?, string?)>();
        listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == IntegrationFailureMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
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

            sink.Enqueue((provider, failureClass));
        });
        listener.Start();
        return sink;
    }
}
