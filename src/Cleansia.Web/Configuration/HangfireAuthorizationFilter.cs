using Hangfire.Dashboard;

namespace Cleansia.Web.Configuration;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // In production, you should implement proper authentication
        // For now, allow access only in development or to authenticated admin users

        // Option 1: Only in Development
        #if DEBUG
        return true;
        #else
        // Option 2: Require authentication and admin role
        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("Admin");
        #endif
    }
}
