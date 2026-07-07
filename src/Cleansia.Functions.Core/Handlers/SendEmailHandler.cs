using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Realizes the account-creation / password-reset email off the request path. The four auth handlers
/// record this intent post-commit; this consumer resolves the template by <see cref="EmailType"/> and
/// sends via the existing <see cref="IEmailService"/>, preserving the language the producer chose.
///
/// Idempotent via <see cref="IIdempotencyGuard"/> in ACT-THEN-CLAIM mode (at-least-once): non-claiming
/// check on the deterministic key → send → claim. A FAILED send leaves the key unclaimed so the queue
/// retry genuinely retries — the previous claim-then-act permanently lost any email whose send failed
/// after the claim. The accepted residual is a rare duplicate email, never a lost one; claim-then-act
/// stays mandatory for consumers whose effect is not safely repeatable (anything money-shaped).
/// Dual-reads the bare in-flight payload at the deploy boundary, synthesizing the same key from the
/// payload. Classifies failures: a malformed / business-rejected body acks (no throw); an
/// infra/transport fault throws so the runtime retries to maxDequeueCount and then dead-letters.
/// </summary>
public class SendEmailHandler(
    IEmailService emailService,
    IIdempotencyGuard idempotencyGuard,
    ITenantProvider tenantProvider,
    ILogger<SendEmailHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        SendEmailMessage? message;
        string? envelopeTenantId;
        try
        {
            (message, envelopeTenantId) = ReadPayload(messageText);
        }
        catch (JsonException ex)
        {
            // Never log the payload — it carries the recipient email and a live confirmation/reset code.
            logger.LogWarning(ex, "Discarding email message: malformed body (permanent)");
            return;
        }

        if (message is null)
        {
            logger.LogWarning("Discarding email message: empty payload (permanent)");
            return;
        }

        var missingFields = new[]
            {
                (Name: nameof(message.UserId), Value: message.UserId),
                (Name: nameof(message.Email), Value: message.Email),
                (Name: nameof(message.Code), Value: message.Code),
            }
            .Where(f => string.IsNullOrEmpty(f.Value))
            .Select(f => f.Name)
            .ToList();

        if (missingFields.Count > 0)
        {
            logger.LogWarning(
                "Discarding {EmailType} email message with missing required fields (permanent): {MissingFields}",
                message.EmailType, string.Join(", ", missingFields));
            return;
        }

        var messageKey = MessageKeys.Email(message.EmailType, message.UserId, MessageKeys.HashCode(message.Code));

        if (await idempotencyGuard.HasProcessedAsync(messageKey, ct))
        {
            logger.LogInformation("Email {MessageKey} already sent, skipping (idempotent)", messageKey);
            return;
        }

        try
        {
            var tenantId = !string.IsNullOrEmpty(envelopeTenantId) ? envelopeTenantId : message.TenantId;
            if (!string.IsNullOrEmpty(tenantId))
            {
                tenantProvider.SetTenantOverride(tenantId);
            }

            await SendAsync(message, ct);

            logger.LogInformation("Sent {EmailType} email to user {UserId} (key {MessageKey})",
                message.EmailType, message.UserId, messageKey);
        }
        catch (Exception ex)
        {
            // The key is still unclaimed, so this redelivery genuinely re-attempts the send.
            logger.LogError(ex,
                "Transient/infra failure sending {EmailType} email for user {UserId} — will retry via queue",
                message.EmailType, message.UserId);
            throw;
        }

        try
        {
            await idempotencyGuard.MarkProcessedAsync(messageKey, ct);
        }
        catch (Exception ex)
        {
            // The email IS sent; throwing here would force an immediate redelivery — a guaranteed
            // duplicate. Log and ack: the residual is a rare duplicate IF this message is redelivered
            // while its key remains unclaimed, which is the accepted worst case for a notification email.
            logger.LogWarning(ex,
                "Sent {EmailType} email to user {UserId} but failed to record the idempotency claim (key {MessageKey}) — acking; a redelivery may duplicate this email",
                message.EmailType, message.UserId, messageKey);
        }
    }

    private Task SendAsync(SendEmailMessage message, CancellationToken ct) => message.EmailType switch
    {
        EmailType.ConfirmationEmail =>
            emailService.SendEmailConfirmationAsync(message.Email, message.UserName, message.Code, message.LanguageCode, ct),
        EmailType.ResetPassword =>
            emailService.SendResetPasswordEmailAsync(message.Email, message.UserName, message.Code, message.LanguageCode, ct),
        _ => throw new InvalidOperationException($"Unsupported email type for the send-email queue: {message.EmailType}"),
    };

    private static (SendEmailMessage? Message, string? EnvelopeTenantId) ReadPayload(string messageText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<SendEmailMessage>>(messageText, JsonOptions);
            if (envelope?.Payload is { UserId.Length: > 0 } payload)
            {
                return (payload, envelope.TenantId);
            }
        }
        catch (JsonException)
        {
            // Fall through to the bare-payload read below.
        }

        return (JsonSerializer.Deserialize<SendEmailMessage>(messageText, JsonOptions), null);
    }
}
