using Cleansia.Core.AppServices.Features.Bookings;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Daily at 02:00 UTC, materialize the next 7 days of recurring bookings into
/// concrete order rows. No-op until templates exist (no UI to create them yet
/// — the foundation ships with Cleansia Plus's "recurring bookings" perk
/// when product launches it).
/// </summary>
public class MaterializeRecurringBookingsHandler(
    IMediator mediator,
    ILogger<MaterializeRecurringBookingsHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
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
