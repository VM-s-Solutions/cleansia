using Hangfire.Dashboard;

namespace Cleansia.Web.Customer.Configuration;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // In production, require authentication and admin role
        // In development, allow access for debugging

        #if DEBUG
        return true;
        #else
        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("Admin");
        #endif
    }
}
