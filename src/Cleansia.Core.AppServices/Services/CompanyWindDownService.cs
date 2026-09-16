using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Services;

public sealed class CompanyWindDownService(
    ITenantRepository tenantRepository,
    ITenantProvider tenantProvider,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IQueueClient queueClient,
    ICampaignProgressStore campaignProgressStore,
    IOrderRepository orderRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    IPlatformOrderCancellation platformOrderCancellation,
    IRecurringBookingTemplateRepository recurringBookingTemplateRepository,
    IUserMembershipRepository userMembershipRepository,
    IStripeClient stripeClient,
    ICreditAccountRepository creditAccountRepository,
    IPayPeriodRepository payPeriodRepository,
    IPayPeriodBackgroundService payPeriodBackgroundService,
    TimeProvider timeProvider,
    ILogger<CompanyWindDownService> logger) : ICompanyWindDownService
{
    private const int NoticePageSize = 200;
    private const string CreditDischargeNote = "company wind-down";
    private const string PeriodCloseNote = "Company wind-down";
    private const string AnonymisedEmailSuffix = "@anonymized.local";

    // InProgress is left out on purpose: a clean under way finishes, and the admin's status override
    // closes one whose cleaner can no longer sign in.
    private static readonly OrderStatus[] CancellableStatuses = [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.OnTheWay];

    private static readonly OrderStatus[] OpenStatuses = [.. OrderAvailability.OfferableStatuses];

    private static readonly HashSet<string> SupportedLocales =
        new(StringComparer.OrdinalIgnoreCase) { "en", "cs", "sk", "uk", "ru" };

    public async Task<CompanyWindDownRunSummary> RunAsync(string tenantId, CancellationToken cancellationToken)
    {
        tenantProvider.SetTenantOverride(tenantId);

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return Skip(tenantId, "the company does not exist");
        }

        if (!tenant.IsWindDownRequested)
        {
            return Skip(tenantId, "no wind-down date is set");
        }

        if (tenant.IsFrozen)
        {
            return Skip(tenantId, "the company is frozen for archive");
        }

        var startedOn = timeProvider.GetUtcNow();
        tenant.StartWindDownRun(startedOn);
        await unitOfWork.CommitAsync(cancellationToken);

        var notices = await SendNoticesAsync(tenant, cancellationToken);
        var (cancelledOrderIds, refunded, refundFailures) = await CancelOrdersAsync(tenant, cancellationToken);
        var (redriven, redriveFailures) = await RedriveRefundsAsync(tenant, cancelledOrderIds, cancellationToken);
        var paused = await PauseTemplatesAsync(cancellationToken);
        var memberships = await CancelMembershipsAsync(cancellationToken);
        var discharged = tenant.IsDeactivated
            ? await DischargeCreditAsync(tenant, startedOn, cancellationToken)
            : 0;
        var (periodsClosed, periodSkippedBecause) = await CloseLastPeriodAsync(tenant, cancellationToken);

        tenant.RecordWindDownRun(timeProvider.GetUtcNow());
        await unitOfWork.CommitAsync(cancellationToken);

        var summary = new CompanyWindDownRunSummary(
            Ran: true,
            SkippedBecause: null,
            NoticesEnqueued: notices,
            OrdersCancelled: cancelledOrderIds.Count,
            Refunded: refunded,
            RefundsRedriven: redriven,
            RefundFailures: refundFailures + redriveFailures,
            TemplatesPaused: paused,
            MembershipsCancelled: memberships,
            CreditAccountsDischarged: discharged,
            PeriodsClosed: periodsClosed,
            PeriodStepSkippedBecause: periodSkippedBecause);

        // Error, not Information: the Functions host's Sentry integration drops Warning to a
        // breadcrumb, and a company closing is a thing a person must see.
        logger.LogError(
            "Company wind-down run for {TenantId} (from {WindDownFrom}, deactivated: {Deactivated}): "
                + "{Notices} notices enqueued, {Cancelled} orders cancelled, {Refunded} refunded, "
                + "{Redriven} refunds re-driven, {RefundFailures} refund failures, {Paused} templates paused, "
                + "{Memberships} memberships cancelled at period end, {Discharged} credit accounts discharged, "
                + "{PeriodsClosed} pay periods closed{PeriodSkipped}",
            tenantId, tenant.WindDownFrom, tenant.IsDeactivated,
            notices, cancelledOrderIds.Count, refunded, redriven, summary.RefundFailures, paused, memberships, discharged,
            periodsClosed, periodSkippedBecause is null ? string.Empty : $" (period step skipped: {periodSkippedBecause})");

        return summary;
    }

    private CompanyWindDownRunSummary Skip(string tenantId, string reason)
    {
        logger.LogError("Company wind-down message for {TenantId} discarded: {Reason}", tenantId, reason);
        return CompanyWindDownRunSummary.Skipped(reason);
    }

    /// <summary>
    /// Every live customer and every approved cleaner, told first and by e-mail, straight onto the
    /// send-email queue page by page. The code is the request instant, so the consumer's own
    /// idempotency key makes a re-run of the same request send nothing twice while a later wind-down
    /// after a reactivation is announced again; the cursor lets a run that died resume past the pages
    /// it finished.
    /// </summary>
    private async Task<int> SendNoticesAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var requestedOn = tenant.WindDownRequestedOn!.Value;
        var code = requestedOn.UtcDateTime.ToString("yyyyMMddHHmmss");
        var campaignId = $"wind-down-notice:{tenant.Id}:{code}";

        var progress = await campaignProgressStore.GetAsync(campaignId, cancellationToken);
        if (progress.IsComplete)
        {
            return 0;
        }

        var recipients = userRepository.GetQueryable()
            .AsNoTracking()
            .Where(u => u.IsActive
                && u.IsEmailConfirmed
                && !u.Email.EndsWith(AnonymisedEmailSuffix)
                && (u.Profile == UserProfile.Customer
                    || (u.Profile == UserProfile.Employee
                        && u.Employee != null
                        && u.Employee.ContractStatus == ContractStatus.Approved)));

        var enqueued = 0;
        var lastUserId = progress.LastProcessedUserId;
        while (true)
        {
            var after = lastUserId;
            var pageQuery = after is null ? recipients : recipients.Where(u => string.Compare(u.Id, after) > 0);

            var page = await pageQuery
                .OrderBy(u => u.Id)
                .Take(NoticePageSize)
                .Select(u => new NoticeRecipient(u.Id, u.Email, u.FirstName, u.LastName, u.PreferredLanguageCode, u.Profile))
                .ToListAsync(cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            lastUserId = page[^1].Id;

            foreach (var recipient in page)
            {
                var emailType = recipient.Profile == UserProfile.Employee
                    ? EmailType.CompanyWindDownCleaner
                    : EmailType.CompanyWindDownCustomer;
                var messageKey = MessageKeys.Email(emailType, recipient.Id, MessageKeys.HashCode(code));

                await queueClient.SendAsync(
                    QueueNames.SendEmail,
                    new QueueEnvelope<SendEmailMessage>(
                        messageKey,
                        tenant.Id,
                        new SendEmailMessage(
                            emailType,
                            recipient.Email,
                            $"{recipient.FirstName} {recipient.LastName}".Trim(),
                            code,
                            ResolveLocale(recipient.PreferredLanguageCode),
                            recipient.Id,
                            tenant.Id)),
                    cancellationToken);
                enqueued++;
            }

            await campaignProgressStore.AdvanceAsync(campaignId, lastUserId, cancellationToken);

            if (page.Count < NoticePageSize)
            {
                break;
            }
        }

        await campaignProgressStore.MarkCompleteAsync(campaignId, cancellationToken);
        return enqueued;
    }

    /// <summary>
    /// Every open booking on or after the last day of service in its market's zone — every open
    /// booking at all once the door is closed — that the customer has actually paid for or will pay
    /// in cash. Cancelled and refunded one by one, each committed before the next Stripe call.
    /// </summary>
    private async Task<(HashSet<string> CancelledOrderIds, int Refunded, int RefundFailures)> CancelOrdersAsync(
        Tenant tenant, CancellationToken cancellationToken)
    {
        var candidates = await orderRepository.GetQueryable()
            .AsNoTracking()
            .Where(o => CancellableStatuses.Contains(o.CurrentStatus)
                && (o.PaymentStatus == PaymentStatus.Paid || o.PaymentType == PaymentType.Cash))
            .OrderBy(o => o.CleaningDateTime)
            .Select(o => new OrderCandidate(
                o.Id,
                o.CleaningDateTime,
                o.CustomerAddress != null ? o.CustomerAddress.CountryId : null))
            .ToListAsync(cancellationToken);

        if (!tenant.IsDeactivated)
        {
            var cutoffs = await MarketCutoffsAsync(tenant.WindDownFrom!.Value, cancellationToken);
            var utcCutoff = WindDownCutoff.Utc(tenant.WindDownFrom.Value, timeZoneId: null);
            candidates = candidates
                .Where(c => c.CleaningDateTime >= (c.CountryId is not null && cutoffs.TryGetValue(c.CountryId, out var cutoff) ? cutoff : utcCutoff))
                .ToList();
        }

        var cancelled = new HashSet<string>(StringComparer.Ordinal);
        var refunded = 0;
        var refundFailures = 0;

        foreach (var candidate in candidates)
        {
            // A fresh untracked read right before the cancel bounds a race with a parallel run, or with a
            // cleaner who just started the job, to the Stripe call itself.
            var currentStatus = await orderRepository.GetQueryable()
                .AsNoTracking()
                .Where(o => o.Id == candidate.Id)
                .Select(o => o.CurrentStatus)
                .FirstOrDefaultAsync(cancellationToken);
            if (!CancellableStatuses.Contains(currentStatus))
            {
                continue;
            }

            var order = await orderRepository.GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                    .ThenInclude(ae => ae.Employee)
                .Include(o => o.Currency)
                .FirstOrDefaultAsync(o => o.Id == candidate.Id, cancellationToken);
            if (order is null)
            {
                continue;
            }

            var result = await platformOrderCancellation.CancelAsync(
                order,
                tenant.WindDownRequestedBy!,
                CancelledBy.System,
                OrderCancellationReasons.CompanyWindDown,
                RefundReason.ServiceNotRendered,
                cancellationToken);
            cancelled.Add(order.Id);

            if (result.Refund.Initiated)
            {
                refunded++;
            }
            else if (result.Refund.Attempted)
            {
                refundFailures++;
                logger.LogError(
                    "Company wind-down could not refund order {OrderId}: {Error}; the order stays cancelled and the next run re-drives it",
                    order.Id, result.Refund.FailureMessage);
            }

            await unitOfWork.CommitAsync(cancellationToken);
        }

        return (cancelled, refunded, refundFailures);
    }

    /// <summary>
    /// A card order an earlier run cancelled whose money has not come back yet: the refund key resolves
    /// to the pending row and Stripe replays once. An order cancelled by this very run is left to the
    /// next one — a refusal seconds old is not answered by asking again seconds later. Empty on a
    /// company whose refunds all succeeded.
    /// </summary>
    private async Task<(int Redriven, int Failures)> RedriveRefundsAsync(
        Tenant tenant, IReadOnlySet<string> cancelledThisRun, CancellationToken cancellationToken)
    {
        var pending = await orderRepository.GetQueryable()
            .AsNoTracking()
            .Where(o => o.CurrentStatus == OrderStatus.Cancelled
                && o.CancelledBy == CancelledBy.System
                && o.CancellationReason == OrderCancellationReasons.CompanyWindDown
                && o.PaymentType == PaymentType.Card
                && o.PaymentStatus == PaymentStatus.Paid)
            .OrderBy(o => o.CleaningDateTime)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var redriven = 0;
        var failures = 0;
        foreach (var orderId in pending.Where(id => !cancelledThisRun.Contains(id)))
        {
            var order = await orderRepository.GetQueryable()
                .Include(o => o.Currency)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            if (order is null)
            {
                continue;
            }

            if (!order.HasRefundableChargeSurface)
            {
                failures++;
                logger.LogError(
                    "Company wind-down cannot refund cancelled order {OrderId}: it is card-paid but carries no Stripe charge surface; an admin must settle it",
                    order.Id);
                continue;
            }

            var outcome = await platformOrderCancellation.RefundAsync(
                order, tenant.WindDownRequestedBy!, RefundReason.ServiceNotRendered, cancellationToken);
            if (outcome.Initiated)
            {
                redriven++;
            }
            else
            {
                failures++;
                logger.LogError(
                    "Company wind-down could not re-drive the refund of order {OrderId}: {Error}",
                    order.Id, outcome.FailureMessage);
            }

            await unitOfWork.CommitAsync(cancellationToken);
        }

        return (redriven, failures);
    }

    private async Task<int> PauseTemplatesAsync(CancellationToken cancellationToken)
    {
        var active = await recurringBookingTemplateRepository.GetQueryable()
            .Where(t => t.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var template in active)
        {
            template.Pause();
        }

        if (active.Count > 0)
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }

        return active.Count;
    }

    /// <summary>
    /// Every Plus the company's customers hold, in any currency: a benefit they pay for and can use
    /// nowhere once the company closes. A Stripe refusal leaves the row selected for the next run.
    /// </summary>
    private async Task<int> CancelMembershipsAsync(CancellationToken cancellationToken)
    {
        var active = await userMembershipRepository.GetQueryable()
            .Where(m => m.Status == MembershipStatus.Active && m.CancelledAt == null)
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken);

        var cancelled = 0;
        foreach (var membership in active)
        {
            try
            {
                await stripeClient.CancelSubscriptionAtPeriodEndAsync(membership.StripeSubscriptionId, cancellationToken);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex,
                    "Company wind-down could not cancel membership {MembershipId} at period end; it stays selected for the next run",
                    membership.Id);
                continue;
            }

            membership.MarkCancellationRequested();
            await unitOfWork.CommitAsync(cancellationToken);
            cancelled++;
        }

        return cancelled;
    }

    /// <summary>
    /// Credit is non-cash and expires rather than pays out; once the door is closed it is spendable
    /// nowhere, so every positive balance is discharged through the same conditional statement the
    /// admin discharge uses, with the note that says why.
    /// </summary>
    private async Task<int> DischargeCreditAsync(Tenant tenant, DateTimeOffset runStartedOn, CancellationToken cancellationToken)
    {
        var positive = await creditAccountRepository.GetQueryable()
            .AsNoTracking()
            .Where(a => a.Balance > 0m)
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.Balance })
            .ToListAsync(cancellationToken);

        var discharged = 0;
        foreach (var account in positive)
        {
            var taken = await creditAccountRepository.TryDebitAsync(
                account.Id,
                account.Balance,
                CreditTransactionReason.Expired,
                $"wind-down-credit:{account.Id}:{runStartedOn.UtcDateTime:yyyyMMddHHmmss}",
                tenant.WindDownRequestedBy!,
                cancellationToken,
                note: CreditDischargeNote);
            if (taken)
            {
                discharged++;
            }
        }

        return discharged;
    }

    /// <summary>
    /// The last pay period closes only once the company is deactivated, no order is open and no
    /// completed order still awaits its pay row — a period closed before that would invoice the
    /// cleaner short. A Closed period is never re-invoiced: the body re-e-mails every cleaner, and
    /// a row an allocation failure left uninvoiced is the admin's to settle from the pay-period tools.
    /// </summary>
    private async Task<(int Closed, string? SkippedBecause)> CloseLastPeriodAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        if (!tenant.IsDeactivated)
        {
            return (0, "the company is not deactivated");
        }

        if (await orderRepository.GetQueryable().AnyAsync(o => OpenStatuses.Contains(o.CurrentStatus), cancellationToken))
        {
            return Skipped("an order is still open");
        }

        if (await orderRepository.GetQueryable().AnyAsync(
                o => o.CurrentStatus == OrderStatus.Completed && !o.EmployeePayCalculated, cancellationToken))
        {
            return Skipped("a completed order still awaits its pay calculation");
        }

        var open = await payPeriodRepository.GetQueryable()
            .Where(p => p.Status == PayPeriodStatus.Open)
            .OrderBy(p => p.StartDate)
            .ToListAsync(cancellationToken);

        foreach (var period in open)
        {
            await payPeriodBackgroundService.ClosePeriodAsync(period, PeriodCloseNote, openNext: false, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }

        return (open.Count, null);

        (int, string?) Skipped(string reason)
        {
            logger.LogError("Company wind-down for {TenantId} skipped the pay-period step: {Reason}", tenant.Id, reason);
            return (0, reason);
        }
    }

    private async Task<Dictionary<string, DateTime>> MarketCutoffsAsync(DateOnly windDownFrom, CancellationToken cancellationToken)
    {
        var markets = await countryConfigurationRepository.GetQueryable()
            .AsNoTracking()
            .Select(c => new { c.CountryId, c.TimeZoneId })
            .ToListAsync(cancellationToken);
        return markets.ToDictionary(m => m.CountryId, m => WindDownCutoff.Utc(windDownFrom, m.TimeZoneId), StringComparer.Ordinal);
    }

    private static string ResolveLocale(string? preferredLanguageCode) =>
        !string.IsNullOrWhiteSpace(preferredLanguageCode) && SupportedLocales.Contains(preferredLanguageCode)
            ? preferredLanguageCode.ToLowerInvariant()
            : Constants.Language.English;

    private sealed record NoticeRecipient(
        string Id, string Email, string FirstName, string LastName, string? PreferredLanguageCode, UserProfile Profile);

    private sealed record OrderCandidate(string Id, DateTime CleaningDateTime, string? CountryId);
}
