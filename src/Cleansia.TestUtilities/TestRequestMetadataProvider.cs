using Cleansia.Core.Domain.Repositories;

namespace Cleansia.TestUtilities;

/// <summary>A fixed request context; every member null mirrors a call outside an HTTP request.</summary>
public sealed class TestRequestMetadataProvider(
    string? ipAddress = null,
    string? deviceLabel = null,
    string? deviceId = null) : IRequestMetadataProvider
{
    public string? IpAddress { get; } = ipAddress;

    public string? DeviceLabel { get; } = deviceLabel;

    public string? DeviceId { get; } = deviceId;
}
