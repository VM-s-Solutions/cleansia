using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cleansia.Infra.Database;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.SendGrid;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
[AllowAnonymous]
public class HealthController(
    CleansiaDbContext dbContext,
    IBlobContainerClientFactory blobClientFactory,
    ISendGridClientFactory sendGridClientFactory,
    IStripeClientFactory stripeClientFactory,
    ISendGridConfig sendGridConfig,
    IStripeConfig stripeConfig) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var healthChecks = new List<HealthCheckResult>();
        var overallHealthy = true;

        // 1. Database Health Check
        var dbHealth = await CheckDatabaseHealthAsync(cancellationToken);
        healthChecks.Add(dbHealth);
        if (!dbHealth.IsHealthy) overallHealthy = false;

        // 2. Blob Storage Health Check
        var blobHealth = await CheckBlobStorageHealthAsync();
        healthChecks.Add(blobHealth);
        if (!blobHealth.IsHealthy) overallHealthy = false;

        // 3. SendGrid Health Check
        var sendGridHealth = CheckSendGridConfiguration();
        healthChecks.Add(sendGridHealth);
        if (!sendGridHealth.IsHealthy) overallHealthy = false;

        // 4. Stripe Health Check
        var stripeHealth = CheckStripeConfiguration();
        healthChecks.Add(stripeHealth);
        if (!stripeHealth.IsHealthy) overallHealthy = false;

        var response = new HealthCheckResponse(
            Status: overallHealthy ? "Healthy" : "Unhealthy",
            Timestamp: DateTime.UtcNow,
            Checks: healthChecks
        );

        return overallHealthy ? Ok(response) : StatusCode(503, response);
    }

    private async Task<HealthCheckResult> CheckDatabaseHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try to execute a simple query
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

            return new HealthCheckResult(
                Name: "Database",
                IsHealthy: true,
                Message: "Database connection is healthy",
                ResponseTime: null
            );
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                Name: "Database",
                IsHealthy: false,
                Message: $"Database connection failed: {ex.Message}",
                ResponseTime: null
            );
        }
    }

    private async Task<HealthCheckResult> CheckBlobStorageHealthAsync()
    {
        try
        {
            var startTime = DateTime.UtcNow;

            // Try to access blob storage
            var blobClient = blobClientFactory.GetBlobContainerClient("test");
            // Simple connectivity check - this will throw if credentials are invalid
            _ = blobClient.GetBlobUri("test");

            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return new HealthCheckResult(
                Name: "BlobStorage",
                IsHealthy: true,
                Message: "Blob storage is accessible",
                ResponseTime: (int)responseTime
            );
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                Name: "BlobStorage",
                IsHealthy: false,
                Message: $"Blob storage check failed: {ex.Message}",
                ResponseTime: null
            );
        }
    }

    private HealthCheckResult CheckSendGridConfiguration()
    {
        try
        {
            var isConfigured = !string.IsNullOrEmpty(sendGridConfig.ApiKey);

            return new HealthCheckResult(
                Name: "SendGrid",
                IsHealthy: isConfigured,
                Message: isConfigured ? "SendGrid is configured" : "SendGrid API key is not configured",
                ResponseTime: null
            );
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                Name: "SendGrid",
                IsHealthy: false,
                Message: $"SendGrid configuration check failed: {ex.Message}",
                ResponseTime: null
            );
        }
    }

    private HealthCheckResult CheckStripeConfiguration()
    {
        try
        {
            var isConfigured = !string.IsNullOrEmpty(stripeConfig.SecretKey) &&
                              !string.IsNullOrEmpty(stripeConfig.PublishableKey);

            return new HealthCheckResult(
                Name: "Stripe",
                IsHealthy: isConfigured,
                Message: isConfigured ? "Stripe is configured" : "Stripe keys are not configured",
                ResponseTime: null
            );
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                Name: "Stripe",
                IsHealthy: false,
                Message: $"Stripe configuration check failed: {ex.Message}",
                ResponseTime: null
            );
        }
    }
}

public record HealthCheckResponse(
    string Status,
    DateTime Timestamp,
    IEnumerable<HealthCheckResult> Checks);

public record HealthCheckResult(
    string Name,
    bool IsHealthy,
    string Message,
    int? ResponseTime);
