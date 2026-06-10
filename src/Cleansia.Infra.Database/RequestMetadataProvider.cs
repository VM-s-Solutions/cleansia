using Cleansia.Core.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Infra.Database;

public class RequestMetadataProvider(IHttpContextAccessor httpContextAccessor) : IRequestMetadataProvider
{
    private const string DeviceLabelHeader = "X-Device-Label";
    private const int MaxDeviceLabelLength = 120;
    private const string UserAgentHeader = "User-Agent";
    private const string DeviceIdHeader = "X-Device-Id";
    private const int MaxDeviceIdLength = 64;

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? DeviceLabel
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null) return null;

            var explicitLabel = context.Request.Headers[DeviceLabelHeader].ToString();
            if (!string.IsNullOrWhiteSpace(explicitLabel))
            {
                return Truncate(explicitLabel);
            }

            var userAgent = context.Request.Headers[UserAgentHeader].ToString();
            return string.IsNullOrWhiteSpace(userAgent) ? null : Truncate(userAgent);
        }
    }

    public string? DeviceId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null) return null;

            // No User-Agent fallback: a stable device id has no human-readable analogue.
            // Absent header → null, which makes the token non-matchable by device revoke.
            var deviceId = context.Request.Headers[DeviceIdHeader].ToString();
            if (string.IsNullOrWhiteSpace(deviceId)) return null;

            return deviceId.Length > MaxDeviceIdLength ? deviceId[..MaxDeviceIdLength] : deviceId;
        }
    }

    private static string Truncate(string value) =>
        value.Length > MaxDeviceLabelLength ? value[..MaxDeviceLabelLength] : value;
}
