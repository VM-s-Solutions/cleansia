using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Filters;

/// <summary>
/// Stripe must always see 2xx, or it retries for days and flags an endpoint every company shares. A
/// webhook whose effect the company's frozen books refused (a late chargeback, a period-end flip on
/// a books row) is acknowledged here, its verbatim body dead-lettered under the frozen company from a
/// fresh scope, and the refusal logged at Error — the row is the operations fact (ADR-0064 D3).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ArchivedCompanyWebhookAcknowledgeFilterAttribute : Attribute, IAsyncActionFilter
{
    public const string SourceQueue = "stripe-webhook";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // The action reads the body itself; buffering lets it be read a second time for the row.
        context.HttpContext.Request.EnableBuffering();

        var executed = await next();
        if (executed.Exception is not CompanyArchivedException refusal || executed.ExceptionHandled)
        {
            return;
        }

        var body = await ReadBodyAsync(context.HttpContext.Request, context.HttpContext.RequestAborted);
        await context.HttpContext.RequestServices.GetRequiredService<ArchivedCompanyDeadLetter>()
            .RecordAsync(SourceQueue, body, refusal, context.HttpContext.RequestAborted);

        executed.ExceptionHandled = true;
        executed.Result = new OkResult();
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
