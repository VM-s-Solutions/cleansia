using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <inheritdoc cref="IAdminNotifier"/>
public class AdminNotifier(
    IUserRepository userRepository,
    IUserNotificationRepository userNotificationRepository,
    IAppConfigurationProvider configurationProvider,
    IPendingDispatch pendingDispatch,
    ILogger<AdminNotifier> logger) : IAdminNotifier
{
    public async Task NotifyAsync(AdminEvent adminEvent, CancellationToken cancellationToken)
    {
        var entry = AdminEventCatalog.Find(adminEvent.Key);
        if (string.IsNullOrWhiteSpace(adminEvent.Subject))
        {
            throw new ArgumentException(
                $"Admin event {adminEvent.Key} carries no subject; its e-mail could not be told from another's.",
                nameof(adminEvent));
        }

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

        var audience = AdminRoleSets.For(entry.Audience);
        var recipients = (await userRepository.GetActiveAdministratorsAsync(adminEvent.TenantId, cancellationToken))
            .Where(r => r.AdminRole is { } role && audience.Contains(role))
            .ToList();
        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Admin event {EventKey} for company {TenantId} reached nobody: no active confirmed administrator in {Audience}",
                adminEvent.Key, adminEvent.TenantId, entry.Audience);
            return;
        }

        var argsJson = JsonSerializer.Serialize(adminEvent.Args);
        foreach (var recipient in recipients)
        {
            userNotificationRepository.Add(
                UserNotification.Create(recipient.Id, adminEvent.Key, argsJson, adminEvent.TenantId));
        }

        var mailbox = await configurationProvider.GetAsync(
            adminEvent.TenantId, TenantSettingCatalog.AdminNotificationEmail, cancellationToken);
        IEnumerable<(string Email, string Locale)> addresses = mailbox.Length > 0
            ? [(mailbox, Constants.Language.English)]
            : recipients.Select(r => (r.Email, EmailLocale.Resolve(r.PreferredLanguageCode)));

        foreach (var (email, locale) in addresses)
        {
            var messageKey = MessageKeys.AdminNotificationEmail(adminEvent.Key, adminEvent.Subject, email);
            pendingDispatch.Enqueue(
                QueueNames.SendEmail,
                new QueueEnvelope<SendAdminNotificationEmailMessage>(
                    messageKey,
                    adminEvent.TenantId,
                    new SendAdminNotificationEmailMessage(
                        adminEvent.Key, adminEvent.Subject, adminEvent.Args, email, locale, adminEvent.TenantId)),
                messageKey);
        }
    }
}
