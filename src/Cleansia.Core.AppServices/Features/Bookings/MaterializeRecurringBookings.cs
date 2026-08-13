using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Daily sweep turning active recurring templates into concrete orders 7 days ahead. Running it twice
/// in a day is a no-op for an already-materialized horizon.
///
/// <para><b>A dispatcher, not a worker.</b> It selects candidate template ids and sends one command per
/// template, each in its OWN DI scope — and therefore its own DbContext, change tracker, tenant provider
/// and pending-dispatch buffer. → /flows/booking-and-pricing#recurring-bookings</para>
/// </summary>
public class MaterializeRecurringBookings
{
    public record Command(int HorizonDays = 7) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.HorizonDays).InclusiveBetween(1, 30);
        }
    }

    /// <summary>
    /// <paramref name="TemplatesProcessed"/> counts the templates that completed (including the ones that
    /// legitimately produced no occurrences); <paramref name="TemplatesFailed"/> counts the ones that
    /// threw or returned a failure and were skipped. A healthy tick reports zero failures, so the pair
    /// doubles as the alerting signal for a template that is permanently stuck.
    /// </summary>
    public record Response(int OrdersCreated, int TemplatesProcessed, int TemplatesFailed = 0);

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // One clock for the whole sweep — the per-template command is handed this same instant so a
            // long sweep cannot drift its own selection window.
            var now = DateTime.UtcNow;
            var horizon = now.AddDays(command.HorizonDays);

            // Ids only. Entities loaded HERE would belong to this scope's context, and the per-template
            // command needs them tracked by its own — so the handoff is deliberately just the key.
            var templateIds = await templateRepository.GetQueryableIgnoringTenant()
                .Where(t => t.IsActive
                    && t.StartsOn <= horizon
                    && (t.EndsOn == null || t.EndsOn > now))
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            var ordersCreated = 0;
            var processed = 0;
            var failed = 0;

            foreach (var templateId in templateIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var scope = serviceScopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    var result = await mediator.Send(
                        new MaterializeRecurringBookingTemplate.Command(templateId, now, command.HorizonDays),
                        cancellationToken);

                    if (result.IsSuccess && result.Value != null)
                    {
                        ordersCreated += result.Value.OrdersCreated;
                        processed++;
                    }
                    else
                    {
                        failed++;
                        logger.LogError(
                            "Recurring template {TemplateId} failed to materialize: {Error}. The sweep continues; "
                            + "the template keeps its previous marker and will be retried on the next tick.",
                            templateId,
                            result.Error?.Message ?? "unknown");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Host shutdown is not a template defect — stop the sweep rather than burn through the
                    // remaining templates recording false failures.
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(
                        ex,
                        "Recurring template {TemplateId} threw while materializing. The sweep continues; its "
                        + "scope (and any half-built order in it) is discarded, and the template keeps its "
                        + "previous marker so the next tick retries it.",
                        templateId);
                }
            }

            if (failed > 0)
            {
                logger.LogWarning(
                    "Recurring sweep finished with {Failed} of {Total} templates failing; {Orders} orders created",
                    failed, templateIds.Count, ordersCreated);
            }

            return BusinessResult.Success(new Response(ordersCreated, processed, failed));
        }
    }
}
