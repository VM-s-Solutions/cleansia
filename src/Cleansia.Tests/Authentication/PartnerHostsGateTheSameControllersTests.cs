using System.Reflection;
using Cleansia.Config.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// The two partner hosts serve the same audience through the same features, so a controller that
/// exists on both gates the same way on both.
///
/// <para><b>The asymmetry this was written for.</b> `[RequireCompleteProfile]` — profile complete, an
/// active document, and `ContractStatus` in {Approved, Active} (`RequireCompleteProfileAttribute.cs`)
/// — was class-level on the **web** partner host's Order, Payroll, Dashboard and Dispute controllers
/// and on **nothing** on the mobile partner host. Meanwhile `RegisterEmployee` is `[AllowAnonymous]`
/// and the mobile login gates on `IsActive` and profile alone, with no `ContractStatus` term. So a
/// self-registered, never-approved account could read the job board and order detail on `:5002` while
/// the identical routes on `:5000` returned 403. Found 2026-08-09 by a security read of the browse
/// gate, which is a different question that happens to run through the same door.</para>
///
/// <para><b>Why the fix is parity rather than a login gate.</b> An unapproved cleaner must be able to
/// sign in — that is how they complete their profile and upload documents. What they must not reach is
/// the work. `CheckCurrentEmployee`, which drives both apps' registration-lock screen, is on
/// `EmployeeController`, which is deliberately ungated on both hosts and stays that way.</para>
///
/// <para>This asserts the shape rather than the instance: any future controller added to one partner
/// host and gated, while its twin on the other is not, fails here.</para>
/// </summary>
public class PartnerHostsGateTheSameControllersTests
{
    private static readonly Assembly WebPartner = typeof(Cleansia.Web.Partner.Attributes.PermissionAttribute).Assembly;
    private static readonly Assembly MobilePartner = typeof(Cleansia.Web.Mobile.Partner.Attributes.PermissionAttribute).Assembly;

    private static Dictionary<string, bool> GatedByControllerName(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .ToDictionary(
                t => t.Name,
                t => t.GetCustomAttribute<RequireCompleteProfileAttribute>(inherit: true) != null,
                StringComparer.Ordinal);

    [Fact]
    public void A_Controller_On_Both_Partner_Hosts_Carries_The_Same_Registration_Gate()
    {
        var web = GatedByControllerName(WebPartner);
        var mobile = GatedByControllerName(MobilePartner);

        var shared = web.Keys.Intersect(mobile.Keys, StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        // Anti-vacuity: an empty intersection would satisfy the assertion below in perfect silence,
        // and so would a run in which neither host gates anything.
        Assert.True(shared.Count >= 8, $"only {shared.Count} controllers are shared between the partner hosts");
        Assert.Contains(web, kv => kv.Value);
        Assert.Contains(mobile, kv => kv.Value);

        var divergent = shared
            .Where(name => web[name] != mobile[name])
            .Select(name => $"{name}: web={(web[name] ? "gated" : "open")}, mobile={(mobile[name] ? "gated" : "open")}")
            .ToList();

        Assert.True(divergent.Count == 0,
            "These controllers exist on both partner hosts and gate differently, so the same caller "
            + "gets a different answer depending on which client they use:\n  "
            + string.Join("\n  ", divergent));
    }

    /// <summary>
    /// The registration-lock screen's own data source must stay reachable, or an unapproved cleaner is
    /// locked out of the very screen that tells them what is missing. Named rather than implied,
    /// because "gate everything" is the obvious wrong fix and this is what makes it wrong.
    /// </summary>
    [Fact]
    public void The_Employee_Controller_That_Feeds_The_Registration_Lock_Is_Gated_On_Neither_Host()
    {
        Assert.False(GatedByControllerName(WebPartner)["EmployeeController"]);
        Assert.False(GatedByControllerName(MobilePartner)["EmployeeController"]);
    }
}
