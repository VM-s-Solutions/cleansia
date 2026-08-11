using System.Reflection;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Tests.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Cleansia.Tests.Features.EmployeeDocuments;

/// <summary>
/// Which hosts reach the document-upload rule.
///
/// <para>The gap this ticket closed was open on two hosts at once, which is the argument against a
/// per-controller attribute: an attribute is what the second host forgets. The bound lives in
/// <c>SaveMyDocuments.Validator</c> instead — proven to bite by
/// <see cref="SaveMyDocumentsValidatorTests"/> — so what remains to prove is that no host reaches the
/// upload by any other route, since MediatR selects the validator by command type alone.</para>
///
/// <para>The set is enumerated off the live host assemblies rather than restated, so a third host
/// surfacing the upload joins it automatically instead of quietly not being covered; the expected pair
/// is named so that LOSING one — a rename, or a host-local re-implementation carrying its own command
/// shape and therefore its own rules — reads as a change rather than as an empty loop.</para>
/// </summary>
public class SaveMyDocumentsRouteCoverageTests
{
    private static readonly string[] ExpectedActions =
    [
        "Cleansia.Web.Mobile.Partner.EmployeeController.SaveMyDocuments",
        "Cleansia.Web.Partner.EmployeeController.SaveMyDocuments"
    ];

    [Fact]
    public void Every_Host_Action_Taking_A_Document_Upload_Dispatches_The_Validated_Command()
    {
        var actions = WireSurface.HostAssemblies()
            .SelectMany(assembly => assembly.GetTypes()
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(action => action.GetCustomAttributes().OfType<HttpMethodAttribute>().Any())
                .Where(action => action.GetParameters().Any(p => p.ParameterType == typeof(SaveMyDocuments.Command)))
                .Select(action => $"{assembly.GetName().Name}.{action.DeclaringType!.Name}.{action.Name}"))
            .Order()
            .ToList();

        Assert.Equal(ExpectedActions, actions);
    }
}
