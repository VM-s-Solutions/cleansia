using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using System.Globalization;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Queue.Abstractions;
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
/// <para><b>Idempotency is the existing-order check, not the watermark.</b>
/// <see cref="RecurringBookingTemplate.LastMaterializedFor"/> is a RESUME POINTER: it says where to start
/// deriving, and <see cref="RecurringBookingTemplate.UpdateSchedule"/> deliberately clears it, because an
/// edited schedule can put the next occurrence EARLIER than the previously materialized one and an
/// un-cleared marker would make that occurrence unreachable forever. While the marker was also the only
/// duplicate guard, that clear re-emitted every occurrence already sitting inside the horizon — a second
/// priced order, and on a card template a second charge, for a slot the customer booked once. So the
/// guard is a query for orders this template already spawned AT THOSE INSTANTS, and the marker is free to
/// keep meaning only "resume here".</para>
///
/// <para>The commit atom is unaffected: all of a template's occurrences and its marker land in one
/// transaction, so a template that throws keeps its old marker and the next tick recomputes the same
/// occurrences and retries it.</para>
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
        ICountryConfigurationRepository countryConfigurationRepository,
        ICurrencyResolutionService currencyResolutionService,
        IOrderRepository orderRepository,
        IOrderPricingCalculator pricingCalculator,
        IOrderFactory orderFactory,
        IUserMembershipRepository userMembershipRepository,
        IReceivableRepository receivableRepository,
        IOperatorTenantResolver operatorTenantResolver,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger,
        INotificationProducer notificationProducer) : ICommandHandler<Command, Response>
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

            // Membership and saved-address reads belong to the template owner's account.
            tenantProvider.ClearTenantOverride();
            if (!string.IsNullOrEmpty(template.User.TenantId))
            {
                tenantProvider.SetTenantOverride(template.User.TenantId);
            }

            // Entitlement and the durable lapse notice belong to the account. Existing occurrences
            // and the authored schedule remain intact while the membership is unpaid.
            var entitled = await userMembershipRepository
                .GetEntitledForUserNoTrackingAsync(template.UserId, cancellationToken);

            if (entitled == null)
            {
                var membership = await userMembershipRepository.GetLatestPaidForUserAsync(template.UserId, cancellationToken);
                if (template.IsActive && membership?.TryMarkRecurringPauseNotificationSent(now) == true)
                {
                    var sequence = membership.RecurringPauseNotificationSequence.ToString(CultureInfo.InvariantCulture);
                    await notificationProducer.NotifyAsync(template.UserId, NotificationEventCatalog.RecurringPaused,
                        new Dictionary<string, string>
                        {
                            ["membershipId"] = membership.Id,
                            ["pauseSequence"] = sequence,
                        }, membership.TenantId, MessageKeys.RecurringPauseSubject(membership.Id, sequence), cancellationToken);
                    await unitOfWork.CommitAsync(cancellationToken);
                }
                logger.LogInformation(
                    "Template {TemplateId} skipped: owner {UserId} has no paid Cleansia Plus membership. "
                    + "The schedule is preserved and resumes if they resubscribe",
                    template.Id, template.UserId);
                return BusinessResult.Success(new Response(0));
            }

            if (!BookingPolicy.IsBookableTimeOfDay(template.TimeOfDay))
            {
                logger.LogWarning(
                    "Template {TemplateId} skipped: its start {TimeOfDay} is outside the bookable window. "
                    + "The schedule is preserved and resumes once its owner moves it to a bookable time",
                    template.Id, template.TimeOfDay);
                return BusinessResult.Success(new Response(0));
            }

            if (!await CustomerCashStanding.OwesNothingAsync(receivableRepository, template.UserId, cancellationToken))
            {
                logger.LogInformation(
                    "Template {TemplateId} skipped: its owner owes an open receivable. "
                    + "The schedule is preserved and resumes once it is paid or written off",
                    template.Id);
                return BusinessResult.Success(new Response(0));
            }

            // Resolve the template's address, fail-soft. Its market's clock is the one the template's day and
            // time are written in.
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

            var marketZone = await TimeZoneResolution.ForMarketAsync(
                countryConfigurationRepository, address.CountryId, cancellationToken);
            var occurrences = ComputeOccurrences(template, now, horizon, marketZone).ToList();
            if (occurrences.Count == 0)
            {
                return BusinessResult.Success(new Response(0));
            }

            // The duplicate guard. Asked once for the whole candidate set rather than once per occurrence,
            // and keyed on the pair the materializer itself writes — (RecurringTemplateId, CleaningDateTime),
            // served by IX_Orders_RecurringTemplateId. Three properties are load-bearing:
            //
            //   * The tenant filter is IGNORED, matching how the template above was loaded. A template id
            //     is already tenant-scoped, so this narrows nothing — but a filter that hid an existing
            //     order would fail OPEN, which is the duplicate charge this exists to refuse.
            //   * The instant is matched EXACTLY, never by day or by window. Both sides are whole minutes
            //     built by ComputeOccurrences, so equality is the question "did we already spawn THIS
            //     occurrence" and nothing wider. A coarser key would silently swallow a legitimate
            //     time-of-day reschedule.
            //   * Order STATUS is not consulted. "Already materialized" is a fact about the sweep, not
            //     about the order's later lifecycle: excluding cancelled rows would let a template edit
            //     resurrect an occurrence the customer cancelled, or one AutoCancelStaleRecurringOrders
            //     retracted an hour before the slot.
            // This read is the fast path, not the guarantee. The guarantee is the unique index
            // IX_Orders_RecurringTemplateId_CleaningDateTime, which speaks at commit: if two sweeps ever
            // run concurrently, the loser's insert is refused and this tick fails, rather than a second
            // order — and on a card template a second charge — reaching the customer. A failed tick
            // self-heals on the next one, which reads the committed row and skips the occurrence.
            var alreadyMaterialized = (await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.RecurringTemplateId == template.Id && occurrences.Contains(o.CleaningDateTime))
                .Select(o => o.CleaningDateTime)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var pending = occurrences.Where(o => !alreadyMaterialized.Contains(o)).ToList();

            if (alreadyMaterialized.Count > 0)
            {
                logger.LogInformation(
                    "Template {TemplateId}: {Skipped} of {Candidates} occurrences in the horizon already have "
                    + "an order and were skipped",
                    template.Id, alreadyMaterialized.Count, occurrences.Count);
            }

            // The marker is the LAST candidate in the window, not the last one created — a skipped
            // occurrence is materialized too, and resuming before it would re-derive and re-query the same
            // window on every tick with the customer's LastMaterializedFor pinned at null.
            var resumeFrom = occurrences[^1];

            if (pending.Count == 0)
            {
                template.MarkMaterializedFor(resumeFrom);
                await unitOfWork.CommitAsync(cancellationToken);
                return BusinessResult.Success(new Response(0));
            }

            // THE SERVICE ADDRESS'S COUNTRY'S CURRENCY, the same rule CreateOrder stamps a one-off
            // booking with (owner ruling 2026-09-12). Fail-closed pricing is the backstop: a currency
            // the template's items are not priced in makes OrderFactory throw, and the per-template
            // scope confines that failure to this template's tick.
            //
            // Resolved inside THIS scope on purpose: the Currency entity is handed to the order factory
            // and ends up referenced by rows this scope's context tracks. A Currency loaded by the outer
            // sweep's context would be a foreign tracked instance here.
            var currency = await currencyResolutionService.ResolveCurrencyForCountryAsync(
                address.CountryId, cancellationToken);

            // Re-resolve the address market on each tick; a closed market creates no occurrences.
            var operatorResolution = await operatorTenantResolver.ResolveAsync(address.CountryId, cancellationToken);
            if (!operatorResolution.IsMarket || operatorResolution.OperatorTenantId is null)
            {
                logger.LogWarning(
                    "Template {TemplateId} skipped: country {CountryId} of its address is not a serviced market with an "
                    + "operating company. The schedule is preserved and resumes if the market reopens",
                    template.Id, address.CountryId);
                return BusinessResult.Success(new Response(0));
            }

            // This calculation supplies the shared raw subtotal. The factory applies the surcharge
            // separately from each occurrence's lead time; a dated price here would charge it twice.
            // Recurring templates carry no extras.
            var rawSubtotalResult = await pricingCalculator.CalculateAsync(
                template.SelectedServiceIds,
                template.SelectedPackageIds,
                Array.Empty<string>(),
                template.Rooms,
                template.Bathrooms,
                template.DirtinessLevel,
                currency.Id,
                cleaningDateUtc: null,
                userId: null,
                nowUtc: now,
                cancellationToken);

            // A cash template authored before the one-cleaner rule. Owner ruling 2026-09-24: never switch
            // its payment for the customer and never charge a card; create nothing until they correct it,
            // which UpdateSchedule does, clearing the marker. The marker is left alone here so a later
            // catalogue edit that makes the selection eligible again loses no occurrence in the window.
            var requiredEmployees = OrderDuration.RequiredEmployees(rawSubtotalResult.EstimatedDurationMinutes);
            if (template.PaymentType == PaymentType.Cash
                && !BookingPolicy.AllowsCash(!string.IsNullOrEmpty(template.UserId), requiredEmployees))
            {
                logger.LogWarning(
                    "Template {TemplateId} skipped: it pays cash for a job needing {RequiredEmployees} cleaners. "
                    + "The schedule is preserved and resumes once its owner moves it to card or a one-cleaner selection",
                    template.Id, requiredEmployees);
                return BusinessResult.Success(new Response(0));
            }

            var customerName = string.Join(" ",
                new[] { template.User.FirstName, template.User.LastName }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            tenantProvider.SetTenantOverride(operatorResolution.OperatorTenantId);

            var ordersCreated = 0;
            foreach (var occurrence in pending)
            {
                var input = new CreateOrderInput(
                    UserId: template.UserId,
                    CustomerName: customerName,
                    CustomerEmail: template.User.Email,
                    CustomerPhone: template.User.PhoneNumber ?? string.Empty,
                    Address: address,
                    Rooms: template.Rooms,
                    Bathrooms: template.Bathrooms,
                    SelectedExtraSlugs: [],
                    CleaningDate: occurrence,
                    PaymentType: template.PaymentType,
                    Currency: currency,
                    SelectedServiceIds: template.SelectedServiceIds,
                    SelectedPackageIds: template.SelectedPackageIds,
                    RawSubtotal: rawSubtotalResult.TotalPrice,
                    NowUtc: now,
                    // Explicitly null, not omitted: a recurring occurrence never draws an express
                    // waiver as a RULE, not as an accident of the template shape carrying no time.
                    ReservedExpressWaiver: null,
                    OperatorTenantId: operatorResolution.OperatorTenantId,
                    PromoDiscountAmount: 0m,
                    PromoCodeId: null,
                    // Unfiltered on purpose: this sweep has no user session, and the factory's
                    // resolver re-runs every gate per occurrence — so a lapsed membership costs the
                    // hold and the push, never the cleaning. Reject where someone can react;
                    // degrade where nobody can.
                    PreferredEmployeeId: template.PreferredEmployeeId,
                    RecurringTemplateId: template.Id,
                    DirtinessLevel: template.DirtinessLevel);

                var order = await orderFactory.CreateAsync(input, cancellationToken);
                if (template.EarlyPerformanceConsentedOn is { } consentedOn)
                {
                    order.RecordEarlyPerformanceConsent(
                        template.EarlyPerformanceConsentTextVersion!,
                        consentedOn,
                        template.EarlyPerformanceConsentClient!,
                        template.EarlyPerformanceConsentIpAddress,
                        template.EarlyPerformanceConsentDeviceLabel);
                }

                ordersCreated++;
            }

            template.MarkMaterializedFor(resumeFrom);

            // Committed HERE and not left to the UnitOfWork pipeline, for the same reason it was before
            // the per-scope split: CleansiaDbContext stamps TenantId on every Added ITenantEntity from the
            // tenant that is ambient AT COMMIT TIME, and the tests drive this handler directly, without a
            // pipeline to commit for them. The occurrences and the LastMaterializedFor marker land in this
            // one transaction — that is the atom. The ambient tenant is switched to the market's operator
            // first, so the occurrences' children (their status rows) land beside the occurrences; the
            // template's marker keeps the template's company.
            tenantProvider.SetTenantOverride(operatorResolution.OperatorTenantId);
            await unitOfWork.CommitAsync(cancellationToken);

            return BusinessResult.Success(new Response(ordersCreated));
        }

        /// <summary>
        /// The CANDIDATE UTC instants for this template in the window from now plus the minimum lead time to
        /// the horizon. The template's day and time are the market's wall clock, so the walk runs over dates
        /// in <paramref name="marketZone"/> and each date is converted on its own — a 10:00 schedule stays
        /// 10:00 across a daylight-saving change. <see cref="RecurringBookingTemplate.LastMaterializedFor"/>
        /// only moves the start of the derivation forward; it is not the duplicate guard, and it is null on
        /// every tick that follows an edit. Whether a candidate already has an order is decided by the caller.
        /// </summary>
        public static IEnumerable<DateTime> ComputeOccurrences(
            RecurringBookingTemplate template,
            DateTime now,
            DateTime horizon,
            TimeZoneInfo marketZone)
        {
            var earliest = now.AddHours(BookingPolicy.ExpressLeadTimeHours);
            var today = MarketDate(now, marketZone);
            var dates = template.Frequency == RecurrenceFrequency.Monthly
                ? MonthlyDates(template, today, marketZone)
                : StepDates(template, today, marketZone);

            foreach (var date in dates)
            {
                var occurrence = MarketTimeToUtc(date, template.TimeOfDay, marketZone);
                if (occurrence > horizon)
                {
                    yield break;
                }

                if (occurrence >= earliest
                    && occurrence >= template.StartsOn
                    && (template.EndsOn == null || occurrence <= template.EndsOn))
                {
                    yield return occurrence;
                }
            }
        }

        /// <summary>
        /// Weekly and biweekly visits fall a whole number of steps after the first scheduled date. Counted
        /// from <see cref="RecurringBookingTemplate.StartsOn"/> rather than from today, so an edit — which
        /// clears the resume pointer — keeps a fortnightly schedule on its own weeks.
        /// </summary>
        private static IEnumerable<DateOnly> StepDates(
            RecurringBookingTemplate template, DateOnly today, TimeZoneInfo marketZone)
        {
            var stepDays = template.Frequency == RecurrenceFrequency.Biweekly ? 14 : 7;
            var anchor = FirstScheduledDate(template, marketZone);
            var from = ResumeFrom(template, anchor, today, marketZone);

            var steps = Math.Max(0, (from.DayNumber - anchor.DayNumber + stepDays - 1) / stepDays);
            var date = anchor.AddDays(steps * stepDays);
            while (true)
            {
                yield return date;
                date = date.AddDays(stepDays);
            }
        }

        /// <summary>
        /// Monthly is the nth weekday of each month (owner ruling 2026-09-28), n read off the first scheduled
        /// date: a schedule that began on the 2nd Thursday stays on the 2nd Thursday, and one that began on
        /// a 5th weekday takes the last, since most months have none. Derived from
        /// <see cref="RecurringBookingTemplate.StartsOn"/> rather than stepped from the previous visit, so an
        /// edit — which clears the resume pointer — keeps the cadence.
        /// </summary>
        private static IEnumerable<DateOnly> MonthlyDates(
            RecurringBookingTemplate template, DateOnly today, TimeZoneInfo marketZone)
        {
            var anchor = FirstScheduledDate(template, marketZone);
            var ordinal = (anchor.Day - 1) / 7 + 1;
            var from = ResumeFrom(template, anchor, today, marketZone);

            var month = new DateOnly(from.Year, from.Month, 1);
            while (true)
            {
                var date = NthWeekdayOfMonth(month, template.DayOfWeek, ordinal);
                if (date >= from)
                {
                    yield return date;
                }

                month = month.AddMonths(1);
            }
        }

        private static DateOnly FirstScheduledDate(RecurringBookingTemplate template, TimeZoneInfo marketZone)
        {
            var date = MarketDate(template.StartsOn, marketZone);
            while (date.DayOfWeek != template.DayOfWeek)
            {
                date = date.AddDays(1);
            }

            return date;
        }

        private static DateOnly ResumeFrom(
            RecurringBookingTemplate template, DateOnly anchor, DateOnly today, TimeZoneInfo marketZone)
        {
            var from = template.LastMaterializedFor.HasValue
                ? MarketDate(template.LastMaterializedFor.Value, marketZone).AddDays(1)
                : anchor;
            return from < today ? today : from;
        }

        private static DateOnly NthWeekdayOfMonth(DateOnly firstOfMonth, DayOfWeek day, int ordinal)
        {
            var first = firstOfMonth.AddDays(((int)day - (int)firstOfMonth.DayOfWeek + 7) % 7);
            if (ordinal < 5)
            {
                return first.AddDays(7 * (ordinal - 1));
            }

            var fifth = first.AddDays(28);
            return fifth.Month == firstOfMonth.Month ? fifth : first.AddDays(21);
        }

        private static DateOnly MarketDate(DateTime utc, TimeZoneInfo marketZone) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), marketZone));

        private static DateTime MarketTimeToUtc(DateOnly date, TimeOnly time, TimeZoneInfo marketZone)
        {
            var local = date.ToDateTime(time, DateTimeKind.Unspecified);

            // ConvertTimeToUtc throws on a wall-clock time the spring change skips, which would fail the
            // template's whole tick; read with the offset in force before the gap, it moves forward by it.
            return marketZone.IsInvalidTime(local)
                ? DateTime.SpecifyKind(local - marketZone.GetUtcOffset(local.AddDays(-1)), DateTimeKind.Utc)
                : TimeZoneInfo.ConvertTimeToUtc(local, marketZone);
        }
    }
}
