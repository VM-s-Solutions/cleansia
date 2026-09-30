using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// Where Stripe sends the browser back to.
///
/// <c>StripeClient.CreateCheckoutSessionAsync</c> builds the order Checkout Session's
/// <c>success_url</c> and <c>cancel_url</c> from <c>Stripe:SuccessUrlBase</c> /
/// <c>Stripe:CancelUrlBase</c>, and Stripe Checkout's header back-arrow navigates to the cancel one.
/// Those two values shipped pointing at <c>http://localhost:4200</c> — the PARTNER dev app — while
/// the customer app serves on 4202. Partner has no <c>checkout</c> route, so its <c>**</c> catch-all
/// answered a paying customer with a partner-branded not-found page. Every CI job was green: no test
/// anywhere read these two keys.
///
/// The port is asserted against <c>apps/cleansia.app/project.json</c> rather than a literal, because
/// a literal here would simply be a second place to get it wrong. Reading both sides is what makes
/// this a drift check instead of a copy of the bug.
///
/// The Customer host mints the order Checkout Session. A receivable's pay link returns to the customer
/// app's order page too, and it is minted on three hosts: the Customer and Customer Mobile hosts, where
/// the customer asks for it, and every host the Stripe webhook lands on — Customer, Customer Mobile and
/// Partner — which e-mails one after a declined off-session charge. Those three must therefore return
/// to the customer app, and to the same place: the first pay link of a receivable is keyed on the
/// receivable, and Stripe refuses a reused key whose request differs.
/// </summary>
public class StripeRedirectConfigTests
{
    private static readonly string[] Hosts =
        ["Cleansia.Web.Customer", "Cleansia.Web.Mobile.Customer", "Cleansia.Web.Partner"];

    public static TheoryData<string> CheckoutHosts => new(Hosts);

    public static TheoryData<string, string> Environments
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var host in Hosts)
            {
                data.Add(host, "Development");
                data.Add(host, "Production");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CheckoutHosts))]
    public void Development_Redirects_Land_On_The_Customer_App(string host)
    {
        var customerAppPort = CustomerAppServePort();
        foreach (var (key, expectedPath) in new[] { ("SuccessUrlBase", "checkout/success"), ("CancelUrlBase", "checkout/cancel") })
        {
            var url = new Uri(LoadStripeValue(host, "Development", key));

            // THE DEFECT, as an assertion: 4200 is the partner app, 4201 is admin, 4202 is the customer
            // app — and only the customer app has a `checkout` route.
            Assert.Equal(customerAppPort, url.Port);
            Assert.Equal("localhost", url.Host);
            Assert.Equal($"/{expectedPath}", url.AbsolutePath);
        }
    }

    /// <summary>
    /// Success and cancel must land in the SAME app. Splitting them is how one of the pair gets
    /// fixed and the other is forgotten — which is exactly the state this file was written in.
    /// </summary>
    [Theory]
    [MemberData(nameof(Environments))]
    public void Both_Redirects_Share_One_Origin(string host, string environment)
    {
        var success = new Uri(LoadStripeValue(host, environment, "SuccessUrlBase"));
        var cancel = new Uri(LoadStripeValue(host, environment, "CancelUrlBase"));

        Assert.Equal(success.GetLeftPart(UriPartial.Authority), cancel.GetLeftPart(UriPartial.Authority));
    }

    [Theory]
    [MemberData(nameof(CheckoutHosts))]
    public void Production_Redirects_Are_Public_Https(string host)
    {
        foreach (var key in new[] { "SuccessUrlBase", "CancelUrlBase" })
        {
            var url = new Uri(LoadStripeValue(host, "Production", key));

            Assert.Equal(Uri.UriSchemeHttps, url.Scheme);
            Assert.DoesNotContain("localhost", url.Host);
        }
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Every_Host_That_Mints_A_Pay_Link_Returns_To_The_Same_Place(string environment)
    {
        foreach (var key in new[] { "SuccessUrlBase", "CancelUrlBase" })
        {
            var values = Hosts.Select(host => LoadStripeValue(host, environment, key)).Distinct().ToList();

            Assert.True(values.Count == 1, $"Stripe:{key} differs across the pay-link hosts in {environment}: {string.Join(", ", values)}");
        }
    }

    /// <summary>
    /// The paths the customer app actually routes. `CleansiaCustomerRoute.CHECKOUT_SUCCESS` is
    /// 'checkout/success' and CHECKOUT_CANCEL is 'checkout/cancel'; a base pointing at a path the
    /// app does not serve produces the same not-found page as the wrong port did.
    /// </summary>
    [Theory]
    [MemberData(nameof(Environments))]
    public void Redirect_Paths_Match_The_Customer_Routes(string host, string environment)
    {
        Assert.Equal(
            "/checkout/success",
            new Uri(LoadStripeValue(host, environment, "SuccessUrlBase")).AbsolutePath);
        Assert.Equal(
            "/checkout/cancel",
            new Uri(LoadStripeValue(host, environment, "CancelUrlBase")).AbsolutePath);
    }

    // Composed the way the host composes it: base file, then the environment file.
    private static string LoadStripeValue(string hostProject, string environment, string key)
    {
        var solutionDir = FindSolutionDirectory(AppContext.BaseDirectory);
        Assert.False(solutionDir is null, "Could not locate the solution directory from the test base directory.");

        var basePath = Path.Combine(solutionDir!, hostProject, "appsettings.json");
        Assert.True(File.Exists(basePath), $"Host settings not found: {basePath}");
        var envPath = Path.Combine(solutionDir!, hostProject, $"appsettings.{environment}.json");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(basePath)
            .AddJsonFile(envPath, optional: true)
            .Build();

        var value = configuration[$"Stripe:{key}"];
        Assert.False(string.IsNullOrWhiteSpace(value), $"Stripe:{key} is not set for {hostProject}/{environment}.");
        return value!;
    }

    /// <summary>
    /// The customer SPA's own dev port, read from the Nx project that serves it. Nx owns this
    /// number; the backend only has to agree with it.
    /// </summary>
    private static int CustomerAppServePort()
    {
        var solutionDir = FindSolutionDirectory(AppContext.BaseDirectory)!;
        var projectJson = Path.Combine(
            solutionDir, "Cleansia.App", "apps", "cleansia.app", "project.json");
        Assert.True(File.Exists(projectJson), $"Nx project file not found: {projectJson}");

        using var document = JsonDocument.Parse(File.ReadAllText(projectJson));
        var port = document.RootElement
            .GetProperty("targets")
            .GetProperty("serve")
            .GetProperty("options")
            .GetProperty("port")
            .GetInt32();

        return port;
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
