using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Enums;
using Cleansia.TestUtilities.MockDataFactories.Users;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// TC-REVOKE-NOW-4 (claim layer, ADR-0026 D1): the device_id claim is emitted only when a device id
/// is supplied. The two mint sites (login / refresh) decide what to pass; this pins the shape.
/// </summary>
public class SetClaimsDeviceIdTests
{
    [Fact]
    public void Device_id_claim_is_emitted_when_supplied()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Employee });

        var claims = user.SetClaims(employeeId: null, deviceId: "device-A").ToList();

        var deviceClaim = claims.SingleOrDefault(c => c.Type == AuthExtensions.DeviceIdClaimType);
        Assert.NotNull(deviceClaim);
        Assert.Equal("device-A", deviceClaim!.Value);
    }

    [Fact]
    public void No_device_id_claim_when_absent()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });

        var claims = user.SetClaims(employeeId: null, deviceId: null).ToList();

        Assert.DoesNotContain(claims, c => c.Type == AuthExtensions.DeviceIdClaimType);
    }

    [Fact]
    public void No_device_id_claim_for_empty_device_id()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });

        var claims = user.SetClaims(employeeId: null, deviceId: string.Empty).ToList();

        Assert.DoesNotContain(claims, c => c.Type == AuthExtensions.DeviceIdClaimType);
    }
}
