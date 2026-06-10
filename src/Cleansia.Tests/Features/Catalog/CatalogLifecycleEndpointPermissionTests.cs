using Cleansia.Core.AppServices.Authentication;
using Cleansia.Web.Admin.Controllers;

namespace Cleansia.Tests.Features.Catalog;

/// <summary>
/// Authorization contract (ADR-0001): the catalog lifecycle endpoints REUSE the
/// existing update permissions — no new Policy.* consts — and those permissions still resolve to
/// AdminOnly in the live map. The logical permission is read from the [Permission] constructor
/// argument, mirroring <c>AnonymousAllowListExhaustivenessTests</c>.
/// </summary>
public class CatalogLifecycleEndpointPermissionTests
{
    private static string? PermissionOf(Type controller, string action) =>
        controller.GetMethod(action)!
            .GetCustomAttributesData()
            .Where(d => d.AttributeType == typeof(Cleansia.Web.Admin.Attributes.PermissionAttribute))
            .Select(d => d.ConstructorArguments[0].Value as string)
            .SingleOrDefault();

    [Theory]
    [InlineData(typeof(AdminServiceController), "DeactivateService", Policy.CanUpdateService)]
    [InlineData(typeof(AdminServiceController), "ActivateService", Policy.CanUpdateService)]
    [InlineData(typeof(AdminPackageController), "DeactivatePackage", Policy.CanUpdatePackage)]
    [InlineData(typeof(AdminPackageController), "ActivatePackage", Policy.CanUpdatePackage)]
    [InlineData(typeof(AdminCurrencyController), "SetDefaultCurrency", Policy.CanUpdateCurrency)]
    public void LifecycleEndpoint_Reuses_The_Existing_Update_Permission(Type controller, string action, string expectedPermission)
    {
        Assert.Equal(expectedPermission, PermissionOf(controller, action));
    }

    [Theory]
    [InlineData(Policy.CanUpdateService)]
    [InlineData(Policy.CanUpdatePackage)]
    [InlineData(Policy.CanUpdateCurrency)]
    public void Reused_Permission_Still_Resolves_To_AdminOnly(string permission)
    {
        Assert.Equal(PhysicalPolicy.AdminOnly, permission.ToPhysicalPolicy());
    }
}
