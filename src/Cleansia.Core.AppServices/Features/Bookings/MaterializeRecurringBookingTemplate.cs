using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using System.Globalization;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Notifications;
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
        ICurrencyResolutionService currencyResolutionService,
        IOrderRepository orderRepository,
        IOrderPricingCalculator pricingCalculator,
        IOrderFactory orderFactory,
        IUserMembershipRepository userMembershipRepository,
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

            var occurrences = ComputeOccurrences(template, now, horizon).ToList();
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
                currency.Id,
                cleaningDateUtc: null,
                // Priced as a guest — null user, null cleaning date — so this background job cannot
                // spend the member's monthly express waivers on occurrences they never asked to be
                // express. It reaches here only for a PAID member (the entitlement gate above), so the
                // guest price is now a deliberate no-waiver choice rather than the lapsed-member
                // fallback it used to be.
                userId: null,
                nowUtc: now,
                cancellationToken);

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
                    RecurringTemplateId: template.Id);

                await orderFactory.CreateAsync(input, cancellationToken);
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
        /// The CANDIDATE UTC instants for this template in the [now, horizon] window.
        /// <see cref="RecurringBookingTemplate.LastMaterializedFor"/> only moves the start of the
        /// derivation forward; it is not the duplicate guard, and it is null on every tick that follows an
        /// edit. Whether a candidate already has an order is decided by the caller.
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
