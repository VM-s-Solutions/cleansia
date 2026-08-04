using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// The per-template ATOM of the recurring sweep. <see cref="MaterializeRecurringBookings"/> selects the
/// candidate template ids and dispatches one of these per template, each in its OWN DI scope.
///
/// <para><b>Why this is a separate command and not a <c>try/catch</c> inside the sweep loop.</b> The
/// naive catch-and-continue is unsafe here, and unsafe in a way that looks correct in review:
/// <list type="bullet">
///   <item><c>CleansiaDbContext.Rollback()</c> sets every tracked entry to <c>Unchanged</c>, and
///   <c>Added -> Unchanged</c> is NOT <c>Detached</c>. A half-built order from the failed template stops
///   being an insert but STAYS in the change tracker as a phantom EXISTING row, which every later
///   iteration then drags along — and the next per-template commit would try to UPDATE a row that was
///   never inserted. Catch-and-continue via <c>Rollback()</c> ships that bug while looking like it
///   detached the wreckage.</item>
///   <item>Without a rollback it is worse: the half-built order is still <c>Added</c>, so the NEXT
///   template's commit persists it.</item>
/// </list>
/// One scope per template makes the question moot. The failed template's <c>DbContext</c> — change
/// tracker, half-built order and all — is disposed with its scope and never observed again; the next
/// template starts from a genuinely empty tracker. That is the property a <c>catch</c> cannot buy.</para>
///
/// <para><b>Idempotency is unchanged by this split.</b> <see cref="RecurringBookingTemplate.LastMaterializedFor"/>
/// is written per OCCURRENCE, but it has always been COMMITTED per template — all of a template's
/// occurrences and its marker land in one transaction, so the durable atom was already the template, not
/// the occurrence. This restructure moves the atom's boundary from "one commit inside a shared context"
/// to "one scope, one context, one commit", which is the same atom with the shared-tracker hazard
/// removed. A template that throws leaves its marker exactly where it was, so the next tick recomputes
/// the same occurrences and retries it; templates that already succeeded replay as no-ops because their
/// marker moved past the horizon.</para>
/// </summary>
public class MaterializeRecurringBookingTemplate
{
    /// <summary>
    /// <paramref name="NowUtc"/> is passed in rather than read from the clock here so that every template
    /// in one sweep shares a single "now" — otherwise the occurrence window would drift across a long
    /// sweep and a template processed late could compute a different horizon than the query that selected
    /// it.
    /// </summary>
    public record Command(string TemplateId, DateTime NowUtc, int HorizonDays) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TemplateId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.HorizonDays).InclusiveBetween(1, 30);
        }
    }

    public record Response(int OrdersCreated);

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        ISavedAddressRepository savedAddressRepository,
        IAddressRepository addressRepository,
        ICurrencyRepository currencyRepository,
        IOrderPricingCalculator pricingCalculator,
        IOrderFactory orderFactory,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = command.NowUtc;
            var horizon = now.AddDays(command.HorizonDays);

            // Ignoring the tenant filter is what lets a system job with no JWT find the row at all; the
            // override set from the loaded TenantId below is the other half of that contract (see
            // IRepository.GetQueryableIgnoringTenant).
            var template = await templateRepository.GetQueryableIgnoringTenant()
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == command.TemplateId, cancellationToken);

            if (template == null)
            {
                // Benign race: the id came from a snapshot taken microseconds earlier and the template was
                // deleted in between. Nothing to materialize is a successful no-op, not a sweep failure.
                logger.LogWarning(
                    "Template {TemplateId} disappeared between selection and materialization; skipping",
                    command.TemplateId);
                return BusinessResult.Success(new Response(0));
            }

            // Scoped ITenantProvider, so this override is confined to this template's scope and cannot
            // leak into the next one even if this handler throws. The clear is belt-and-braces for hosts
            // that hand out a longer-lived provider.
            tenantProvider.ClearTenantOverride();
            if (!string.IsNullOrEmpty(template.TenantId))
            {
                tenantProvider.SetTenantOverride(template.TenantId);
            }

            var occurrences = ComputeOccurrences(template, now, horizon).ToList();
            if (occurrences.Count == 0)
            {
                return BusinessResult.Success(new Response(0));
            }

            // Resolved inside THIS scope on purpose: the Currency entity is handed to the order factory
            // and ends up referenced by rows this scope's context tracks. A Currency loaded by the outer
            // sweep's context would be a foreign tracked instance here.
            var defaultCurrency = await currencyRepository.GetDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("No default currency configured");

            // Resolve the template's address, fail-soft.
            var saved = await savedAddressRepository.GetByIdAsync(template.SavedAddressId, cancellationToken);
            if (saved == null)
            {
                logger.LogWarning(
                    "Template {TemplateId} references missing SavedAddress {SavedAddressId}; skipping",
                    template.Id, template.SavedAddressId);
                return BusinessResult.Success(new Response(0));
            }
            var address = saved.Address
                ?? await addressRepository.GetByIdAsync(saved.AddressId, cancellationToken);
            if (address == null)
            {
                logger.LogWarning(
                    "SavedAddress {SavedAddressId} references missing Address {AddressId}; skipping template {TemplateId}",
                    saved.Id, saved.AddressId, template.Id);
                return BusinessResult.Success(new Response(0));
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
                // A lapsed membership must not stop a schedule, and a live one must not have this
                // background job spend the member's monthly express waivers on occurrences they never
                // asked to be express. Both fall out of pricing the occurrence as a guest: null user,
                // null cleaning date, no waiver resolved, full price.
                userId: null,
                nowUtc: now,
                cancellationToken);

            var customerName = string.Join(" ",
                new[] { template.User.FirstName, template.User.LastName }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            var ordersCreated = 0;
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
                    NowUtc: now,
                    // Explicitly null, not omitted: a recurring occurrence never draws an express
                    // waiver as a RULE, not as an accident of the template shape carrying no time.
                    ReservedExpressWaiver: null,
                    PromoDiscountAmount: 0m,
                    PromoCodeId: null,
                    // Unfiltered on purpose: this sweep has no user session, and the factory's
                    // resolver re-runs every gate per occurrence — so a lapsed membership costs the
                    // hold and the push, never the cleaning. Reject where someone can react;
                    // degrade where nobody can.
                    PreferredEmployeeId: template.PreferredEmployeeId,
                    RecurringTemplateId: template.Id);

                await orderFactory.CreateAsync(input, cancellationToken);
                template.MarkMaterializedFor(occurrence);
                ordersCreated++;
            }

            // Committed HERE and not left to the UnitOfWork pipeline, for the same reason it was before
            // the per-scope split: CleansiaDbContext stamps TenantId on every Added ITenantEntity from the
            // tenant that is ambient AT COMMIT TIME, and the tests drive this handler directly, without a
            // pipeline to commit for them. The occurrences and the LastMaterializedFor marker land in this
            // one transaction — that is the atom.
            await unitOfWork.CommitAsync(cancellationToken);

            return BusinessResult.Success(new Response(ordersCreated));
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
