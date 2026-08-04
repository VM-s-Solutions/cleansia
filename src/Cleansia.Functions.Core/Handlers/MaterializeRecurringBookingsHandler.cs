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
            // TemplatesFailed is the signal that one template is permanently stuck: the sweep now isolates
            // each template in its own scope and carries on past a failure, so a bad row no longer shows up
            // as a failed invocation — it shows up here, as a non-zero count that stays non-zero.
            logger.LogInformation(
                "MaterializeRecurringBookings completed; processed {Templates} templates, created {Orders} orders, "
                + "{FailedTemplates} templates failed and will be retried next tick",
                result.Value.TemplatesProcessed,
                result.Value.OrdersCreated,
                result.Value.TemplatesFailed);
        }
        else
        {
            logger.LogError(
                "MaterializeRecurringBookings failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
