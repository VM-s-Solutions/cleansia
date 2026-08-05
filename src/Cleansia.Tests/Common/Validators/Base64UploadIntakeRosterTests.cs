using System.Reflection;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Tests.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Cleansia.Tests.Common.Validators;

/// <summary>
/// Every route on every host that accepts a base64 payload, enumerated rather than remembered.
///
/// <para>Two consecutive hardening tickets each fixed the intake they were pointed at and each left a
/// sibling writing the same container under the same rules it had before — <c>SaveMyDocuments</c> was
/// hardened while <c>UpdateEmployee</c>, four files away, kept storing a client-declared type into the
/// same table. The defect was never the missing rule; it was that nothing said how many intakes there
/// are. This is that statement, and it is the only artifact here that would have caught the gap at the
/// time rather than after it.</para>
///
/// <para>A new upload endpoint reddens this test, and the fix is to add the row AFTER deciding which
/// <c>AbstractValidator&lt;BlobFileDto&gt;</c> guards it — the reason the roster is annotated with that
/// choice instead of being a bare list of routes.</para>
/// </summary>
public class Base64UploadIntakeRosterTests
{
    /// <summary>
    /// Host action → the rule its payload passes through. <c>SaveOrderPhotos</c> is the one entry with no
    /// <c>BlobFileDto</c> validator of its own: it takes photos from an assigned cleaner and its served
    /// type is clamped on the read path by <c>ServedContentType</c>, so it bounds size and leaves content
    /// to the clamp.
    /// </summary>
    private static readonly string[] ExpectedIntakes =
    [
        "Cleansia.Web.Customer.UserController.UpdateCurrentUser — ImageFileValidator",
        "Cleansia.Web.Mobile.Customer.UserController.UpdateCurrentUser — ImageFileValidator",
        "Cleansia.Web.Mobile.Partner.EmployeeController.SaveMyDocuments — DocumentFileValidator",
        "Cleansia.Web.Mobile.Partner.EmployeeController.UpdateEmployee — DocumentFileValidator",
        "Cleansia.Web.Mobile.Partner.OrderController.SavePhotos — BlobFileSize only",
        "Cleansia.Web.Mobile.Partner.UserController.UpdateCurrentUser — ImageFileValidator",
        "Cleansia.Web.Partner.EmployeeController.SaveMyDocuments — DocumentFileValidator",
        "Cleansia.Web.Partner.EmployeeController.UpdateEmployee — DocumentFileValidator",
        "Cleansia.Web.Partner.OrderController.SavePhotos — BlobFileSize only",
        "Cleansia.Web.Partner.UserController.UpdateCurrentUser — ImageFileValidator"
    ];

    [Fact]
    public void Every_Route_Accepting_A_Base64_Payload_Is_On_The_Roster()
    {
        var intakes = WireSurface.HostAssemblies()
            .SelectMany(assembly => assembly.GetTypes()
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(action => action.GetCustomAttributes().OfType<HttpMethodAttribute>().Any())
                .Where(CarriesAnUploadOnTheRequest)
                .Select(action => $"{assembly.GetName().Name}.{action.DeclaringType!.Name}.{action.Name}"))
            .Order()
            .ToList();

        Assert.Equal(
            ExpectedIntakes.Select(entry => entry.Split(" — ")[0]).ToList(),
            intakes);
    }

    private static bool CarriesAnUploadOnTheRequest(MethodInfo action) =>
        action.GetParameters()
            .SelectMany(parameter => WireSurface.Expand(WireSurface.UnwrapCollection(parameter.ParameterType), 0))
            .Contains(typeof(BlobFileDto));
}
