using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Infra.Clients.Stripe;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The two customer-app paths a membership checkout returns to.
///
/// They used to be sent by the browser, which is what made them an open redirect: any authenticated
/// caller could name the page Stripe returned to after payment. Deriving them server-side closed
/// that — and moved two Angular route strings into a .NET infrastructure assembly, where no Angular
/// test, no <c>nx affected</c> run and no compiler can see them.
///
/// That is exactly the blind spot that let <c>Stripe:SuccessUrlBase</c> point at the partner app's
/// port for as long as it did, so the paths are pinned the same way
/// <see cref="StripeRedirectConfigTests"/> pins the port: by reading the other side rather than
/// restating it. Rename <c>CleansiaCustomerRoute.PLUS</c> or move the welcome route and this fails,
/// instead of a customer backing out of payment onto a 404 nobody hears about.
/// </summary>
public class MembershipReturnPathTests
{
    [Fact]
    public void CancelPath_Is_The_Plus_Route_The_Angular_App_Serves()
    {
        var plus = CustomerRouteValue("PLUS");

        Assert.Equal($"/{plus}", PrivateConst("PlusPagePath"));
    }

    [Fact]
    public void SuccessPath_Is_The_Welcome_Route_Under_The_Membership_Mount()
    {
        var membership = CustomerRouteValue("MEMBERSHIP");

        // 'welcome' is a child of the membership mount (profile/src/lib/lib.routes.ts), so the
        // composed path is what the router actually resolves.
        Assert.Contains("path: 'welcome'", ProfileRoutesSource());
        Assert.Equal($"/{membership}/welcome", PrivateConst("MembershipWelcomePath"));
    }

    [Fact]
    public void Both_Paths_Are_Rooted_And_Carry_No_Origin()
    {
        // The origin comes from Stripe:SuccessUrlBase; a path that smuggled one in would silently
        // win over it and send the customer somewhere else entirely.
        foreach (var name in new[] { "PlusPagePath", "MembershipWelcomePath" })
        {
            var path = PrivateConst(name);
            Assert.StartsWith("/", path);
            Assert.DoesNotContain("//", path);
            Assert.False(Uri.IsWellFormedUriString(path, UriKind.Absolute), $"{name} must be a path, not a URL.");
        }
    }

    /// <summary>The compile-time constant StripeClient actually uses, read off the type itself.</summary>
    private static string PrivateConst(string name)
    {
        var field = typeof(StripeClient).GetField(
            name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetField);
        Assert.False(field is null, $"StripeClient.{name} not found — was it renamed?");
        return (string)field!.GetRawConstantValue()!;
    }

    /// <summary>A member of the Angular <c>CleansiaCustomerRoute</c> enum, read from its source.</summary>
    private static string CustomerRouteValue(string member)
    {
        var source = ReadAppFile("libs", "core", "services", "src", "lib", "enums", "routes.enum.ts");

        // Only the customer enum — the partner and admin enums declare some of the same names.
        // Terminated on a closing brace at column 0, NOT the first `}` anywhere: a doc comment in
        // this very enum contains "/r/{code}", and a non-greedy match stopped there, silently
        // truncating the body to the first three members.
        var enumBody = Regex.Match(
            source,
            @"export enum CleansiaCustomerRoute\s*\{(?<body>.*?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(enumBody.Success, "CleansiaCustomerRoute enum not found in routes.enum.ts.");

        var value = Regex.Match(enumBody.Groups["body"].Value, $@"\b{member}\s*=\s*'(?<value>[^']*)'");
        Assert.True(value.Success, $"CleansiaCustomerRoute.{member} not found.");
        return value.Groups["value"].Value;
    }

    private static string ProfileRoutesSource() => ReadAppFile(
        "libs", "cleansia-customer-features", "profile", "src", "lib", "lib.routes.ts");

    private static string ReadAppFile(params string[] relativeParts)
    {
        var solutionDir = FindSolutionDirectory(AppContext.BaseDirectory);
        Assert.False(solutionDir is null, "Could not locate the solution directory.");

        var path = Path.Combine([solutionDir!, "Cleansia.App", .. relativeParts]);
        Assert.True(File.Exists(path), $"Angular source not found: {path}");
        return File.ReadAllText(path);
    }

    // Mirrors DevCorsOriginsConfigTests — walk up until a *.sln is found.
    private static string? FindSolutionDirectory(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
