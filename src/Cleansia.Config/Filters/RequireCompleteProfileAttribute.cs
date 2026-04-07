using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireCompleteProfileAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userSession = context.HttpContext.RequestServices.GetRequiredService<IUserSessionProvider>();
        var employeeRepo = context.HttpContext.RequestServices.GetRequiredService<IEmployeeRepository>();

        var email = userSession.GetUserEmail();
        if (email is null)
        {
            await next();
            return;
        }

        var employee = await employeeRepo.GetByUserEmailAsync(email);
        if (employee is null)
        {
            await next();
            return;
        }

        var isRegistrationComplete =
            employee.IsProfileComplete() &&
            employee.Documents.Any(d => d.IsActive) &&
            (employee.ContractStatus == ContractStatus.Approved || employee.ContractStatus == ContractStatus.Active);

        if (!isRegistrationComplete)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Title = "Registration incomplete",
                Status = 403,
                Detail = "Complete your profile, upload documents, and wait for admin approval before accessing this resource.",
            })
            {
                StatusCode = 403,
            };
            return;
        }

        await next();
    }
}
