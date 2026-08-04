using System.Reflection;
using Cleansia.Core.AppServices.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// T-0526 AC8 — both customer hosts expose the cancellation preview, on the same route and behind the
/// same policy as the cancel it quotes. Two hosts serve the same two clients (web and customer
/// mobile), so an endpoint added to one of them is an endpoint half the customers cannot call, and a
/// policy that drifts apart means a caller who may cancel but may not find out what it costs.
/// The permission is read off the host's own <c>[Permission]</c> attribute by name, mirroring
/// <c>CatalogLifecycleEndpointPermissionTests</c>, because each host declares its own attribute type.
/// </summary>
public class CancellationPreviewEndpointParityTests
{
    private const string PreviewAction = "CancellationPreview";
    private const string CancelAction = "CancelOrder";

    public static TheoryData<Type> CustomerOrderControllers =>
    [
        typeof(Cleansia.Web.Customer.Controllers.OrderController),
        typeof(Cleansia.Web.Mobile.Customer.Controllers.OrderController),
    ];

    private static MethodInfo Action(Type controller, string name) =>
        controller.GetMethod(name)
        ?? throw new InvalidOperationException($"{controller.FullName} does not expose {name}.");

    private static string? PermissionOf(Type controller, string action) =>
        Action(controller, action)
            .GetCustomAttributesData()
            .Where(d => d.AttributeType.Name == "PermissionAttribute")
            .Select(d => d.ConstructorArguments[0].Value as string)
            .SingleOrDefault();

    [Theory]
    [MemberData(nameof(CustomerOrderControllers))]
    public void Both_Customer_Hosts_Expose_The_Preview_As_A_Get_On_The_Same_Route(Type controller)
    {
        var route = Action(controller, PreviewAction).GetCustomAttribute<HttpGetAttribute>();

        Assert.NotNull(route);
        Assert.Equal("CancellationPreview", route.Template);
    }

    [Theory]
    [MemberData(nameof(CustomerOrderControllers))]
    public void The_Preview_Carries_The_Same_Permission_As_The_Cancel_It_Quotes(Type controller)
    {
        Assert.Equal(Policy.CanCancelOrder, PermissionOf(controller, PreviewAction));
        Assert.Equal(PermissionOf(controller, CancelAction), PermissionOf(controller, PreviewAction));
    }
}
