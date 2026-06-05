namespace Cleansia.Tests.RateLimiting.Harness;

/// <summary>
/// Serializes the host-level rate-limiter tests. They each boot a TestServer and exercise an
/// in-process partitioned limiter + a shared static Meter (D8); running them concurrently would let
/// independent partition trees / metric readers interleave. Keep these serial.
/// </summary>
[CollectionDefinition("RateLimiterHost", DisableParallelization = true)]
public class RateLimiterHostCollection { }
