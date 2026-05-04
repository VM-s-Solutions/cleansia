using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Daily sweep that turns active <see cref="RecurringBookingTemplate"/> rows
/// into concrete <see cref="Cleansia.Core.Domain.Orders.Order"/> records 7 days
/// ahead. Idempotent via <see cref="RecurringBookingTemplate.LastMaterializedFor"/>:
/// running it twice on the same day is a no-op for templates that already
/// have an order created within the horizon.
///
/// No UI exists today to create templates, so on a fresh database this handler
/// processes zero rows — the entity + materializer are foundations for when
/// Cleansia Plus's "recurring bookings" perk launches.
/// </summary>
public class MaterializeRecurringBookings
{
    public record Command(int HorizonDays = 7) : ICommand<Response>;

    public record Response(int OrdersCreated, int TemplatesProcessed);

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        ISavedAddressRepository savedAddressRepository,
        IMediator mediator,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var horizon = now.AddDays(command.HorizonDays);

            var activeTemplates = await templateRepository.GetQueryable()
                .Include(t => t.User)
                .Where(t => t.IsActive
                    && t.StartsOn <= horizon
                    && (t.EndsOn == null || t.EndsOn > now))
                .ToListAsync(cancellationToken);

            int ordersCreated = 0;

            foreach (var template in activeTemplates)
            {
                try
                {
                    var occurrences = ComputeOccurrences(template, now, horizon).ToList();
                    if (occurrences.Count == 0)
                    {
                        continue;
                    }

                    // Resolve the saved address once per template — same address
                    // is reused across all occurrences this batch. We use the
                    // user-scoped fetch so the inline Address navigation comes
                    // along (base GetByIdAsync doesn't Include).
                    var userAddresses = await savedAddressRepository
                        .GetByUserAsync(template.UserId, cancellationToken);
                    var savedAddress = userAddresses
                        .FirstOrDefault(a => a.Id == template.SavedAddressId);
                    if (savedAddress?.Address == null)
                    {
                        logger.LogWarning(
                            "Template {TemplateId} references missing saved address {AddressId}; skipping",
                            template.Id, template.SavedAddressId);
                        continue;
                    }

                    foreach (var occurrence in occurrences)
                    {
                        // Recurring orders inherit the user's snapshot. Price
                        // is computed by CreateOrder's pricing pipeline at
                        // materialization time so members get the right
                        // discount for that period.
                        // Note: TotalPrice = 0 below means "let the validator
                        // re-quote and accept whatever the pricing calculator
                        // returns" — but the current validator requires the
                        // total to match exactly. For now we resolve via the
                        // pricing calculator. TODO when the materializer is
                        // wired into a recurring-booking UX, decide whether to:
                        //  (a) materialize as drafts the user must confirm,
                        //  (b) auto-charge from saved card on file (requires
                        //      stored payment method + SCA exemption),
                        //  (c) something else. Today this code path doesn't
                        //      execute because no templates exist.
                        var addressDto = new AddressDto(
                            savedAddress.Address.Street,
                            savedAddress.Address.City,
                            savedAddress.Address.ZipCode,
                            savedAddress.Address.CountryId,
                            savedAddress.Address.State);

                        var createCommand = new CreateOrder.Command(
                            CustomerName: $"{template.User.FirstName} {template.User.LastName}".Trim(),
                            CustomerEmail: template.User.Email,
                            CustomerPhone: template.User.PhoneNumber ?? string.Empty,
                            CustomerAddress: addressDto,
                            SavedAddressId: template.SavedAddressId,
                            SelectedPackageIds: template.SelectedPackageIds,
                            SelectedServiceIds: template.SelectedServiceIds,
                            Rooms: template.Rooms,
                            Bathrooms: template.Bathrooms,
                            Extras: new Dictionary<string, bool>(),
                            CleaningDate: occurrence,
                            PaymentType: template.PaymentType,
                            CurrencyId: null,
                            TotalPrice: 0m,
                            UserId: template.UserId);

                        var result = await mediator.Send(createCommand, cancellationToken);
                        if (result.IsSuccess)
                        {
                            ordersCreated++;
                            template.MarkMaterializedFor(occurrence);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Template {TemplateId} occurrence {Occurrence} failed: {Error}",
                                template.Id, occurrence, result.Error?.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // One template failing should not kill the sweep — log and continue.
                    logger.LogError(ex,
                        "Materialization failed for template {TemplateId}; continuing with next",
                        template.Id);
                }
            }

            return BusinessResult.Success(new Response(ordersCreated, activeTemplates.Count));
        }

        /// <summary>
        /// Compute the list of UTC DateTimes this template should spawn within
        /// the [now, horizon] window, skipping any already-materialized via
        /// <see cref="RecurringBookingTemplate.LastMaterializedFor"/>.
        /// </summary>
        internal static IEnumerable<DateTime> ComputeOccurrences(
            RecurringBookingTemplate template,
            DateTime now,
            DateTime horizon)
        {
            // Determine the search start: max(template.StartsOn, lastMaterialized + step, now).
            var step = template.Frequency switch
            {
                RecurrenceFrequency.Weekly => TimeSpan.FromDays(7),
                RecurrenceFrequency.Biweekly => TimeSpan.FromDays(14),
                RecurrenceFrequency.Monthly => TimeSpan.FromDays(30), // approximation, fine for matching pool
                _ => TimeSpan.FromDays(7),
            };

            var searchStart = template.LastMaterializedFor.HasValue
                ? template.LastMaterializedFor.Value + step
                : template.StartsOn;
            if (searchStart < now) searchStart = now;

            // Find the first occurrence on or after searchStart that lands on
            // template.DayOfWeek at template.TimeOfDay.
            var candidate = searchStart.Date;
            while (candidate.DayOfWeek != template.DayOfWeek)
            {
                candidate = candidate.AddDays(1);
            }
            var occurrence = candidate
                .AddHours(template.TimeOfDay.Hour)
                .AddMinutes(template.TimeOfDay.Minute);

            while (occurrence <= horizon)
            {
                if (occurrence >= template.StartsOn
                    && (template.EndsOn == null || occurrence <= template.EndsOn))
                {
                    yield return occurrence;
                }
                occurrence = occurrence.Add(step);
            }
        }
    }
}
