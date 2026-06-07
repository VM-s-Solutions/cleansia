using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// Records the account-creation / password-reset email as post-commit dispatch intent on the
/// send-email queue. The handlers never send inline — a transport outage degrades the email, never the
/// command. The deterministic key (per generated token) lets a redelivery / duplicate enqueue be
/// recognized as already-done by the consumer.
/// </summary>
internal static class EmailDispatch
{
    public static void EnqueueConfirmation(
        IPendingDispatch pending, User user, string userName, string rawConfirmationToken, string languageCode)
        => Enqueue(pending, EmailType.ConfirmationEmail, user, userName, rawConfirmationToken, languageCode);

    public static void EnqueuePasswordReset(
        IPendingDispatch pending, User user, string userName, string rawResetToken, string languageCode)
        => Enqueue(pending, EmailType.ResetPassword, user, userName, rawResetToken, languageCode);

    private static void Enqueue(
        IPendingDispatch pending, EmailType emailType, User user, string userName, string rawCode, string languageCode)
    {
        var key = MessageKeys.Email(emailType, user.Id, MessageKeys.HashCode(rawCode));
        pending.Enqueue(
            QueueNames.SendEmail,
            new QueueEnvelope<SendEmailMessage>(
                key,
                user.TenantId,
                new SendEmailMessage(emailType, user.Email, userName, rawCode, languageCode, user.Id, user.TenantId)),
            key);
    }
}
