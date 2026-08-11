using System.Data.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// Default <see cref="IPreferredCleanerHoldResolver"/>. Every gate answers the same question — can this
/// cleaner actually act on this offer — and a "no" costs the perk, never the booking.
/// </summary>
public sealed class PreferredCleanerHoldResolver(
    IUserMembershipRepository userMembershipRepository,
    IEmployeeRepository employeeRepository,
    IUserNotificationPreferencesRepository preferencesRepository,
    IDeviceRepository deviceRepository,
    IOrderRepository orderRepository,
    ILogger<PreferredCleanerHoldResolver> logger) : IPreferredCleanerHoldResolver
{
    public async Task<PreferredCleanerOutcome> ResolveAsync(
        string? userId,
        string? preferredEmployeeId,
        string? serviceCountryId,
        DateTime cleaningUtc,
        int estimatedMinutes,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(preferredEmployeeId))
        {
            return PreferredCleanerOutcome.Declined(HoldDeclineReason.NoPreference);
        }

        if (string.IsNullOrEmpty(userId))
        {
            return PreferredCleanerOutcome.Declined(HoldDeclineReason.NoMembership);
        }

        var membership = await userMembershipRepository
            .GetActiveForUserNoTrackingAsync(userId, cancellationToken);
        if (membership is null)
        {
            return PreferredCleanerOutcome.Declined(HoldDeclineReason.NoMembership);
        }

        var cleaner = await employeeRepository.GetByIdAsync(preferredEmployeeId, cancellationToken);
        if (cleaner is null)
        {
            return PreferredCleanerOutcome.Declined(HoldDeclineReason.CleanerNotFound);
        }

        // IsActive, not ContractStatus alone: a departing or GDPR-erased cleaner is soft-deleted and
        // Deactivated(...) leaves the contract status untouched, so an approved-looking row can belong
        // to someone who left the platform.
        var approved = cleaner.IsActive
            && cleaner.ContractStatus is ContractStatus.Approved or ContractStatus.Active;
        if (!approved)
        {
            return PreferredCleanerOutcome.Declined(HoldDeclineReason.CleanerNotApproved);
        }

        if (string.IsNullOrEmpty(cleaner.WorkCountryId)
            || !string.Equals(cleaner.WorkCountryId, serviceCountryId, StringComparison.Ordinal))
        {
            return PreferredCleanerOutcome.Declined(HoldDeclineReason.CleanerCountryMismatch);
        }

        var preferences = await preferencesRepository.GetByUserIdAsync(cleaner.UserId, cancellationToken);
        if (preferences is not null && !preferences.IsAllowed(NotificationCategory.NewJobsAvailable))
        {
            return PreferredCleanerOutcome.Declined(HoldDeclineReason.CleanerMutedNewJobs);
        }

        // Reachability is three facts, not one: the category mute above, the OS-level kill switch, and
        // having no device row at all — the state of every cleaner who works from the partner web SPA.
        var devices = await deviceRepository.GetByUserIdAsync(cleaner.UserId, cancellationToken);
        if (!devices.Any(d => d.NotificationsEnabled))
        {
            return PreferredCleanerOutcome.Declined(HoldDeclineReason.CleanerUnreachableForPush);
        }

        // Last of the eligibility gates because it is the only one costing a range scan — and BEFORE
        // the lead-time gate because a short lead still notifies while a busy cleaner must not.
        if (await IsBusyAtCleaningTimeAsync(preferredEmployeeId, cleaningUtc, estimatedMinutes, cancellationToken))
        {
            return PreferredCleanerOutcome.Declined(HoldDeclineReason.CleanerBusyAtCleaningTime);
        }

        var recipient = new PreferredCleanerRecipient(cleaner.UserId, cleaner.TenantId);

        var hold = BookingPolicy.ComputePreferredHold(cleaningUtc, nowUtc);
        if (hold <= TimeSpan.Zero)
        {
            return PreferredCleanerOutcome.NotifyOnly(HoldDeclineReason.ShortLeadTime, recipient);
        }

        return PreferredCleanerOutcome.Granted(nowUtc.Add(hold), recipient);
    }

    private async Task<bool> IsBusyAtCleaningTimeAsync(
        string preferredEmployeeId, DateTime cleaningUtc, int estimatedMinutes, CancellationToken cancellationToken)
    {
        try
        {
            var busy = await orderRepository.GetBusyEmployeeIdsInWindowAsync(
                [preferredEmployeeId],
                cleaningUtc,
                cleaningUtc.AddMinutes(estimatedMinutes),
                cancellationToken);

            return busy.Contains(preferredEmployeeId);
        }
        catch (DbException ex)
        {
            // A perk fails closed; a booking never does. Unknown is treated as busy, so the order is
            // still created and the preference is still stored.
            logger.LogWarning(
                ex,
                "Preferred-cleaner slot check failed for employee {EmployeeId}; declining the hold",
                preferredEmployeeId);
            return true;
        }
    }
}
