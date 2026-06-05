using System.Text.Json;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Queue.Abstractions.Messages;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Consumes the per-cleaner fan-out from <c>CompleteOrder</c> and runs
/// <c>CalculateOrderPay.Command</c> so the order's <c>OrderEmployeePay</c>
/// row exists before the next nightly invoice rollup.
///
/// Calls <c>IPayPeriodBackgroundService.EnsureOpenPeriodAsync</c> first so
/// pay-calc never fails with <c>NoActivePeriod</c> on fresh environments —
/// the rest of the validator chain (assignment, pay config, duplicate
/// guard) still applies and rejects cleanly when violated.
///
/// Validator rejections are logged at warning and the message is acked —
/// the request was malformed (e.g. no pay config for that cleaner+service)
/// and retrying won't fix it. Infra failures throw so the queue retries up
/// to <c>maxDequeueCount</c> (host.json).
/// </summary>
public class CalculateOrderPayHandler(
    IMediator mediator,
    IPayPeriodBackgroundService payPeriodService,
    ILogger<CalculateOrderPayHandler> logger)
{
    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        var message = JsonSerializer.Deserialize<CalculateOrderPayMessage>(
            messageText,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException("Failed to deserialize CalculateOrderPayMessage");

        await payPeriodService.EnsureOpenPeriodAsync(ct);

        var result = await mediator.Send(
            new CalculateOrderPay.Command(message.OrderId, message.EmployeeId),
            ct);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "CalculateOrderPay succeeded for order {OrderId} / employee {EmployeeId} → pay row {PayId}",
                message.OrderId,
                message.EmployeeId,
                result.Value?.EmployeePayrollId);
        }
        else
        {
            // Validator rejected (already-calculated, missing config, etc.).
            // Don't throw — retrying won't change the validator's verdict and
            // we don't want to poison-queue a permanent business-rule miss.
            logger.LogWarning(
                "CalculateOrderPay rejected for order {OrderId} / employee {EmployeeId}: {Error}",
                message.OrderId,
                message.EmployeeId,
                result.Error?.Message ?? "unknown");
        }
    }
}
