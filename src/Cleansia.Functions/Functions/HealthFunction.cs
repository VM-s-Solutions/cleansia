using System.Net;
using Cleansia.Config.Health;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Cleansia.Functions.Functions;

// The ONLY HTTP surface on the queue/timer Functions host — an anonymous liveness+readiness probe at
// GET /api/health, wired as the App Service healthCheckPath so the platform auto-recycles an unhealthy
// instance and the HealthCheckStatus metric alert notifies ops. Returns 200 when every dependency probe
// passes, 503 (with the failing probe named) otherwise. Anonymous by design: a health endpoint that
// needs a key can't be pinged by the platform monitor, and it reveals only up/down + dependency names.
public class HealthFunction(FunctionsHealthCheck healthCheck)
{
    [Function("Health")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var report = await healthCheck.CheckAsync(cancellationToken);

        var response = request.CreateResponse();
        // WriteAsJsonAsync writes the body + content-type and sets 200; override the status AFTER so a
        // failed probe surfaces as 503 — what the platform health monitor + the metric alert key on.
        await response.WriteAsJsonAsync(report, cancellationToken);
        response.StatusCode = report.Healthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
        return response;
    }
}
