using Cleansia.Core.AppServices.Features.Bookings;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

/// <summary>
/// Daily at 02:00 UTC, materialize the next 7 days of recurring bookings into
/// concrete order rows. No-op until templates exist (no UI to create them yet
/// — the foundation ships with Cleansia Plus's "recurring bookings" perk
/// when product launches it).
/// </summary>
public class MaterializeRecurringBookingsFunction(
    IMediator mediator,
    ILogger<MaterializeRecurringBookingsFunction> logger)
{
    [Function("MaterializeRecurringBookings")]
    public async Task Run([TimerTrigger("0 */2 * * * *")] TimerInfo timer, CancellationToken ct)
    {
        logger.LogInformation("MaterializeRecurringBookings timer triggered at {Time}", DateTime.UtcNow);
        var result = await mediator.Send(new MaterializeRecurringBookings.Command(HorizonDays: 7), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "MaterializeRecurringBookings completed; processed {Templates} templates, created {Orders} orders",
                result.Value.TemplatesProcessed,
                result.Value.OrdersCreated);
        }
        else
        {
            logger.LogError(
                "MaterializeRecurringBookings failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
