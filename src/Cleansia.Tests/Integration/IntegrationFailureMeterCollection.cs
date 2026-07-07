using Cleansia.Core.Clients.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// Serializes every test class that emits or listens on the process-global
/// <see cref="IntegrationFailureMetrics"/> meter with a REAL provider name ("SendGrid"/"Mapbox"/
/// "Stripe"/"Fcm"). xUnit runs collections in parallel, so a listener asserting on a real provider
/// would otherwise capture a measurement from a parallel class driving the same boundary to failure
/// — and provider-name filtering cannot fix that, because the foreign measurement carries the SAME
/// provider name. <c>DisableParallelization</c> keeps the group from overlapping the rest of the
/// suite, so a future real-name emitter outside this collection still cannot collide with the
/// listeners here. Tests that emit only unique synthetic provider names
/// (<see cref="IntegrationFailureMetricsTests"/>) stay hermetic by filtering and need not join.
/// </summary>
[CollectionDefinition("IntegrationFailureMeter", DisableParallelization = true)]
public class IntegrationFailureMeterCollection { }
