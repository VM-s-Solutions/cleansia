using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Middleware;

/// <summary>
/// Makes an unhandled invocation visible. The isolated worker reports a failed invocation to the
/// Functions host over gRPC and the HOST logs it — in its own process, outside this worker's
/// <c>ILogger</c> pipeline and therefore past the Sentry provider <c>AddSentryMonitoring</c> installs.
/// Since Application Insights was removed nothing reads the host side, so a timer sweep or a queue
/// consumer that throws out of a handler which does not log for itself fails in total silence.
///
/// Log and RE-THROW, never swallow: the rethrow is what still fails the invocation, so the Storage-queue
/// runtime keeps its retry-then-poison behaviour and a timer still reports failure.
/// </summary>
public sealed class FunctionInvocationErrorMiddleware(ILogger<FunctionInvocationErrorMiddleware> logger)
    : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Function {FunctionName} failed (invocation {InvocationId}).",
                context.FunctionDefinition.Name, context.InvocationId);
            throw;
        }
    }
}
