using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
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

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.HorizonDays).InclusiveBetween(1, 30);
        }
    }

    public record Response(int OrdersCreated, int TemplatesProcessed);

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        ISavedAddressRepository savedAddressRepository,
        IAddressRepository addressRepository,
        ICurrencyRepository currencyRepository,
        IOrderPricingCalculator pricingCalculator,
        IOrderFactory orderFactory,
        ITenantProvider tenantProvider,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var horizon = now.AddDays(command.HorizonDays);

            var activeTemplates = await templateRepository.GetQueryableIgnoringTenant()
                .Include(t => t.User)
                .Where(t => t.IsActive
                    && t.StartsOn <= horizon
                    && (t.EndsOn == null || t.EndsOn > now))
                .ToListAsync(cancellationToken);

            // Default currency once per sweep — every template uses it. Templates
            // don't carry a currency today (single-market launch), but if multi-
            // currency lands, switch this to per-template lookup.
            var defaultCurrency = await currencyRepository.GetDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("No default currency configured");

            var ordersCreated = 0;
            foreach (var template in activeTemplates)
            {
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(template.TenantId))
                {
                    tenantProvider.SetTenantOverride(template.TenantId);
                }

                var occurrences = ComputeOccurrences(template, now, horizon).ToList();
                if (occurrences.Count == 0)
                {
                    continue;
                }

                // Resolve the template's address once, fail-soft per-template.
                var saved = await savedAddressRepository.GetByIdAsync(template.SavedAddressId, cancellationToken);
                if (saved == null)
                {
                    logger.LogWarning(
                        "Template {TemplateId} references missing SavedAddress {SavedAddressId}; skipping",
                        template.Id, template.SavedAddressId);
                    continue;
                }
                var address = saved.Address
                    ?? await addressRepository.GetByIdAsync(saved.AddressId, cancellationToken);
                if (address == null)
                {
                    logger.LogWarning(
                        "SavedAddress {SavedAddressId} references missing Address {AddressId}; skipping template {TemplateId}",
                        saved.Id, saved.AddressId, template.Id);
                    continue;
                }

                // Recurring orders are scheduled days/weeks in advance,
                // so the express surcharge never applies — pass null
                // CleaningDate to skip the surcharge check. Extras aren't
                // part of the recurring template today; pass empty.
                var rawSubtotalResult = await pricingCalculator.CalculateAsync(
                    template.SelectedServiceIds,
                    template.SelectedPackageIds,
                    Array.Empty<string>(),
                    template.Rooms,
                    template.Bathrooms,
                    defaultCurrency.Id,
                    cleaningDateUtc: null,
                    cancellationToken);

                var customerName = string.Join(" ",
                    new[] { template.User.FirstName, template.User.LastName }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));

                foreach (var occurrence in occurrences)
                {
                    var input = new CreateOrderInput(
                        UserId: template.UserId,
                        CustomerName: customerName,
                        CustomerEmail: template.User.Email,
                        CustomerPhone: template.User.PhoneNumber ?? string.Empty,
                        Address: address,
                        Rooms: template.Rooms,
                        Bathrooms: template.Bathrooms,
                        Extras: new(),
                        CleaningDate: occurrence,
                        PaymentType: template.PaymentType,
                        Currency: defaultCurrency,
                        SelectedServiceIds: template.SelectedServiceIds,
                        SelectedPackageIds: template.SelectedPackageIds,
                        RawSubtotal: rawSubtotalResult.TotalPrice,
                        PromoDiscountAmount: 0m,
                        PromoCodeId: null,
                        PreferredEmployeeId: null,
                        RecurringTemplateId: template.Id);

                    await orderFactory.CreateAsync(input, cancellationToken);
                    template.MarkMaterializedFor(occurrence);
                    ordersCreated++;
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
