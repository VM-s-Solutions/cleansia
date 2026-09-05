using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Tests.RateLimiting;

public class CoverageProbe
{
    private static readonly string[] Mutating = { "POST", "PUT", "DELETE", "PATCH" };

    [Fact]
    public void Probe()
    {
        var assemblies = new[]
        {
            typeof(Cleansia.Web.Customer.Controllers.PaymentController).Assembly,
            typeof(Cleansia.Web.Mobile.Customer.Controllers.PaymentController).Assembly,
            typeof(Cleansia.Web.Partner.Controllers.PaymentController).Assembly,
            typeof(Cleansia.Web.Mobile.Partner.Controllers.OrderController).Assembly,
            typeof(Cleansia.Web.Admin.Controllers.AdminAuthController).Assembly,
        };

        foreach (var t in assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(ControllerBase).IsAssignableFrom(t))
            .OrderBy(t => t.FullName))
        {
            var actions = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>()
                    .Any(a => a.HttpMethods.Any(v => Mutating.Contains(v, StringComparer.OrdinalIgnoreCase))))
                .ToList();
            if (actions.Count == 0) continue;

            var uncovered = actions.Where(a =>
                a.GetCustomAttribute<DisableRateLimitingAttribute>() is not null
                || (a.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName
                    ?? a.DeclaringType!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName) is null)
                .Select(a => a.Name).ToList();

            Console.WriteLine($"PROBE {t.FullName} mutating={actions.Count} uncovered={uncovered.Count} [{string.Join(",", uncovered)}]");
        }
    }
}
