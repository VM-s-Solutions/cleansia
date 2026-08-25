using System.Reflection;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Tests.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Cleansia.Tests.Common.Validators;

/// <summary>
/// Every route on every host that accepts an uploaded file, enumerated rather than remembered.
///
/// <para>Two consecutive hardening tickets each fixed the intake they were pointed at and each left a
/// sibling writing the same container under the same rules it had before — <c>SaveMyDocuments</c> was
/// hardened while <c>UpdateEmployee</c>, four files away, kept storing a client-declared type into the
/// same table. The defect was never the missing rule; it was that nothing said how many intakes there
/// are. This is that statement, and it is the only artifact here that would have caught the gap at the
/// time rather than after it.</para>
///
/// <para><b>Why the predicate is not "reaches <c>BlobFileDto</c>".</b> It was, and it reported ten routes
/// out of fourteen. <c>UploadOrderPhoto</c> takes a raw <c>byte[]</c> and <c>UploadDisputeEvidence</c> an
/// <c>IFormFile</c>, so both were invisible to a walk keyed on that one DTO while both still derived
/// their stored content type from a client string — the exact defect the roster exists to make
/// countable. A roster that silently under-reports is worse than none, because it reads as coverage; the
/// question it answers is "does a file reach storage from here", and the wire shape a client happens to
/// use is not part of that question.</para>
///
/// <para>A new upload endpoint reddens this test, and the fix is to add the row AFTER deciding which
/// rule guards it — the reason the roster is annotated with that choice instead of being a bare list of
/// routes.</para>
/// </summary>
public class UploadIntakeRosterTests
{
    /// <summary>
    /// Host action → the rule its payload passes through. <c>SaveOrderPhotos</c> is the one entry with no
    /// content rule of its own: it takes photos from an assigned cleaner and its served type is clamped
    /// on the read path by <c>ServedContentType</c>, so it bounds size and leaves content to the clamp.
    /// </summary>
    private static readonly string[] ExpectedIntakes =
    [
        "Cleansia.Web.Customer.DisputeController.UploadEvidence — SniffedContentType(DisputeEvidence)",
        "Cleansia.Web.Customer.UserController.UpdateCurrentUser — ImageFileValidator",
        "Cleansia.Web.Mobile.Customer.DisputeController.UploadEvidence — SniffedContentType(DisputeEvidence)",
        "Cleansia.Web.Mobile.Customer.UserController.UpdateCurrentUser — ImageFileValidator",
        "Cleansia.Web.Mobile.Partner.EmployeeController.ReplaceMyDocument — DocumentFileValidator",
        "Cleansia.Web.Mobile.Partner.EmployeeController.SaveMyDocuments — DocumentFileValidator",
        "Cleansia.Web.Mobile.Partner.EmployeeController.UpdateEmployee — DocumentFileValidator",
        "Cleansia.Web.Mobile.Partner.OrderController.SavePhotos — BlobFileSize only",
        "Cleansia.Web.Mobile.Partner.OrderController.UploadPhoto — SniffedContentType(OrderPhoto)",
        "Cleansia.Web.Mobile.Partner.UserController.UpdateCurrentUser — ImageFileValidator",
        "Cleansia.Web.Partner.EmployeeController.ReplaceMyDocument — DocumentFileValidator",
        "Cleansia.Web.Partner.EmployeeController.SaveMyDocuments — DocumentFileValidator",
        "Cleansia.Web.Partner.EmployeeController.UpdateEmployee — DocumentFileValidator",
        "Cleansia.Web.Partner.OrderController.SavePhotos — BlobFileSize only",
        "Cleansia.Web.Partner.OrderController.UploadPhoto — SniffedContentType(OrderPhoto)",
        "Cleansia.Web.Partner.UserController.UpdateCurrentUser — ImageFileValidator"
    ];

    [Fact]
    public void Every_Route_Accepting_An_Uploaded_File_Is_On_The_Roster()
    {
        var intakes = WalkIntakes();

        // The count first: on an empty or broken walk every set comparison below would agree with an
        // empty roster for the wrong reason, and this is the assertion whose number the ticket cites.
        Assert.Equal(ExpectedIntakes.Length, intakes.Count);

        Assert.Equal(
            ExpectedIntakes.Select(entry => entry.Split(" — ")[0]).ToList(),
            intakes);
    }

    /// <summary>
    /// The four routes the <c>BlobFileDto</c>-only predicate could not see, asserted by name. The roster
    /// above would go green again if someone narrowed the predicate AND deleted the four rows together;
    /// naming them here means that edit has to be deliberate.
    /// </summary>
    [Theory]
    [InlineData("Cleansia.Web.Partner.OrderController.UploadPhoto")]
    [InlineData("Cleansia.Web.Mobile.Partner.OrderController.UploadPhoto")]
    [InlineData("Cleansia.Web.Customer.DisputeController.UploadEvidence")]
    [InlineData("Cleansia.Web.Mobile.Customer.DisputeController.UploadEvidence")]
    public void The_NonBase64_Intakes_Are_Reachable_By_The_Walk(string intake)
    {
        Assert.Contains(intake, WalkIntakes());
    }

    private static List<string> WalkIntakes() =>
        WireSurface.HostAssemblies()
            .SelectMany(assembly => assembly.GetTypes()
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(action => action.GetCustomAttributes().OfType<HttpMethodAttribute>().Any())
                .Where(CarriesAnUploadOnTheRequest)
                .Select(action => $"{assembly.GetName().Name}.{action.DeclaringType!.Name}.{action.Name}"))
            .Order()
            .ToList();

    private static bool CarriesAnUploadOnTheRequest(MethodInfo action) =>
        action.GetParameters().Any(parameter =>
            IsFormFile(WireSurface.UnwrapCollection(parameter.ParameterType)) ||
            WireSurface.Expand(WireSurface.UnwrapCollection(parameter.ParameterType), 0)
                .Any(type => type == typeof(BlobFileDto) || CarriesRawBytes(type)));

    private static bool IsFormFile(Type type) => typeof(IFormFile).IsAssignableFrom(type);

    private static bool CarriesRawBytes(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => property.PropertyType == typeof(byte[]));
}
