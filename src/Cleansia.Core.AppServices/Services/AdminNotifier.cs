using System.Text.Json;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <inheritdoc cref="IAdminNotifier"/>
public class AdminNotifier(
    IUserRepository userRepository,
    IUserNotificationRepository userNotificationRepository,
    ILogger<AdminNotifier> logger) : IAdminNotifier
{
    public async Task NotifyAsync(AdminEvent adminEvent, CancellationToken cancellationToken)
    {
        var entry = AdminEventCatalog.Find(adminEvent.Key);
        var undeclared = adminEvent.Args.Keys.Except(entry.EmailArgOrder, StringComparer.Ordinal).ToList();
        if (undeclared.Count > 0)
        {
            throw new ArgumentException(
                $"Admin event {adminEvent.Key} carries args its catalogue entry does not declare: {string.Join(", ", undeclared)}.",
                nameof(adminEvent));
        }

        var missing = entry.EmailArgOrder.Except(adminEvent.Args.Keys, StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException(
                $"Admin event {adminEvent.Key} lacks args its catalogue entry declares: {string.Join(", ", missing)}.",
                nameof(adminEvent));
        }

        var recipients = await userRepository.GetActiveAdministratorsAsync(adminEvent.TenantId, cancellationToken);
        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Admin event {EventKey} for company {TenantId} reached nobody: no active confirmed administrator",
                adminEvent.Key, adminEvent.TenantId);
            return;
        }

        var argsJson = JsonSerializer.Serialize(adminEvent.Args);
        foreach (var recipient in recipients)
        {
            userNotificationRepository.Add(
                UserNotification.Create(recipient.Id, adminEvent.Key, argsJson, adminEvent.TenantId));
        }
    }
}
