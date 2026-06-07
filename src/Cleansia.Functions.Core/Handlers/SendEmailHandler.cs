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
/// Idempotent via <see cref="IIdempotencyGuard"/> (claim-then-act on the deterministic key, before the
/// terminal send) so a redelivery / duplicate enqueue re-sends nothing. Dual-reads the bare in-flight
/// payload at the deploy boundary, synthesizing the same key from the payload. Classifies failures: a
/// malformed / business-rejected body acks (no throw); an infra/transport fault throws so the runtime
/// retries to maxDequeueCount and then dead-letters.
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
            logger.LogWarning(ex, "Discarding email message: malformed body (permanent). Message: {Message}", messageText);
            return;
        }

        if (message is null
            || string.IsNullOrEmpty(message.UserId)
            || string.IsNullOrEmpty(message.Email)
            || string.IsNullOrEmpty(message.Code))
        {
            logger.LogWarning("Discarding email message with missing required fields (permanent). Message: {Message}", messageText);
            return;
        }

        var messageKey = MessageKeys.Email(message.EmailType, message.UserId, MessageKeys.HashCode(message.Code));

        if (await idempotencyGuard.AlreadyProcessedAsync(messageKey, ct))
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
            logger.LogError(ex,
                "Transient/infra failure sending {EmailType} email for user {UserId} — will retry via queue",
                message.EmailType, message.UserId);
            throw;
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
