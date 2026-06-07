using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// Queue message recorded by the account-creation / password-reset command handlers (Register,
/// RegisterEmployee, ResendConfirmationEmail, RequestPasswordChange) post-commit, and realized by the
/// send-email consumer which resolves the template by <see cref="EmailType"/> and sends via the
/// existing email service.
///
/// <see cref="Code"/> is the RAW confirmation/reset token (the email's payload — the entity persists
/// only its hash). It is the deploy-boundary dual-read source for synthesizing the deterministic key
/// (its hash is the code segment of <see cref="MessageKeys.Email"/>). <see cref="TenantId"/> is sent so
/// the queue consumer (no JWT) can set the cross-tenant override.
/// </summary>
public record SendEmailMessage(
    EmailType EmailType,
    string Email,
    string UserName,
    string Code,
    string LanguageCode,
    string UserId,
    string? TenantId = null);
