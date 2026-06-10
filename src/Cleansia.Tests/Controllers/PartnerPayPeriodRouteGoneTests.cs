using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PartnerPayPeriodController = Cleansia.Web.Partner.Controllers.PayPeriodController;

namespace Cleansia.Tests.Controllers;

/// <summary>
/// T-0171 AC5 — the per-audience-host seam: pay-period MUTATIONS belong ONLY on the Admin host. The
/// Partner host's <see cref="PartnerPayPeriodController"/> used to duplicate the full write surface
/// (Create/Update/Delete/Open/Close); those were AdminOnly-gated (a cleaner got 403, not exploitable)
/// but were redundant write surface. They are removed, leaving only the two read endpoints. The Admin
/// host's <c>AdminPayPeriodController</c> still owns the full write surface.
///
/// Reflects over the controller's action methods (mirrors the host-controller reflection idiom in
/// <c>AnonymousAllowListExhaustivenessTests</c>) and asserts the mutation routes are gone and the read
/// routes remain.
/// </summary>
public class PartnerPayPeriodRouteGoneTests
{
    private static IReadOnlyList<MethodInfo> ActionMethods() =>
        typeof(PartnerPayPeriodController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: false).Any())
            .ToList();

    private static IReadOnlyList<string> RouteTemplates() =>
        ActionMethods()
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: false))
            .Where(a => a.Template is not null)
            .Select(a => a.Template!)
            .ToList();

    [Theory]
    [InlineData("CreatePayPeriod")]
    [InlineData("UpdatePayPeriod")]
    [InlineData("DeletePayPeriod")]
    [InlineData("OpenPayPeriod")]
    [InlineData("ClosePayPeriod")]
    public void Mutation_Action_Method_Is_Gone_From_The_Partner_Host(string actionName)
    {
        var method = typeof(PartnerPayPeriodController)
            .GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);

        Assert.Null(method); // The mutation endpoint no longer exists on the Partner controller.
    }

    [Theory]
    [InlineData("CreatePayPeriod")]
    [InlineData("UpdatePayPeriod")]
    [InlineData("DeletePayPeriod")]
    [InlineData("OpenPayPeriod")]
    [InlineData("ClosePayPeriod")]
    public void Mutation_Route_Template_Is_Gone_From_The_Partner_Host(string routeSegment)
    {
        Assert.DoesNotContain(routeSegment, RouteTemplates());
    }

    [Fact]
    public void The_Partner_Host_Exposes_No_Mutating_Http_Verbs()
    {
        // No POST / PUT / DELETE action survives on the Partner pay-period surface — it is read-only.
        var mutatingVerbs = ActionMethods()
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: false))
            .SelectMany(a => a.HttpMethods)
            .Where(v => v is "POST" or "PUT" or "DELETE" or "PATCH")
            .ToList();

        Assert.Empty(mutatingVerbs);
    }

    [Theory]
    [InlineData("GetPagedPayPeriods")]
    [InlineData("GetPayPeriodById")]
    public void Read_Endpoint_Remains_On_The_Partner_Host(string actionName)
    {
        var method = typeof(PartnerPayPeriodController)
            .GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.Contains(
            method!.GetCustomAttributes<HttpMethodAttribute>(inherit: false),
            a => a.HttpMethods.Contains("GET"));
    }
}
