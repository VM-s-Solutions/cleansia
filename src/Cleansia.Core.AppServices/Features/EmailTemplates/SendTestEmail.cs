using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmailTemplates;

public class SendTestEmail
{
    public record Command(
        string EmailTemplateId,
        string RecipientEmail) : ICommand<Response>;

    public record Response(string MessageId, string RecipientEmail);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IEmailTemplateTranslationRepository emailTemplateRepository)
        {
            RuleFor(x => x.EmailTemplateId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(emailTemplateRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmailTemplateNotFound);

            RuleFor(x => x.RecipientEmail)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .EmailAddress()
                .WithMessage(BusinessErrorMessage.InvalidEmail);
        }
    }

    internal class Handler(
        IEmailTemplateTranslationRepository emailTemplateRepository,
        IEmailService emailService)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = await emailTemplateRepository.GetByIdAsync(command.EmailTemplateId, cancellationToken);

            if (template == null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.EmailTemplateId), BusinessErrorMessage.EmailTemplateNotFound));
            }

            var languageCode = template.Language?.Code ?? Constants.Language.English;
            string messageId;

            switch (template.EmailType)
            {
                case EmailType.ConfirmationEmail:
                    messageId = await emailService.SendEmailConfirmationAsync(
                        command.RecipientEmail,
                        "Test User",
                        "123456",
                        languageCode,
                        cancellationToken);
                    break;

                case EmailType.ResetPassword:
                    messageId = await emailService.SendResetPasswordEmailAsync(
                        command.RecipientEmail,
                        "Test User",
                        "RESET123",
                        languageCode,
                        cancellationToken);
                    break;

                case EmailType.OrderReceipt:
                    messageId = await emailService.SendTestOrderReceiptEmailAsync(
                        command.RecipientEmail,
                        "Test Customer",
                        "ORD-2025-0001",
                        DateTime.UtcNow.ToString("d"),
                        "$99.99",
                        languageCode,
                        cancellationToken);
                    break;

                case EmailType.PeriodClosed:
                    messageId = await emailService.SendPeriodClosedEmailAsync(
                        command.RecipientEmail,
                        "Test Employee",
                        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)),
                        DateOnly.FromDateTime(DateTime.UtcNow),
                        DateTime.UtcNow,
                        "2025-01",
                        languageCode,
                        ct: cancellationToken);
                    break;

                case EmailType.PeriodEndReminder:
                    messageId = await emailService.SendPeriodEndReminderEmailAsync(
                        command.RecipientEmail,
                        "Test Employee",
                        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
                        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                        7,
                        "2025-01",
                        languageCode,
                        cancellationToken);
                    break;

                default:
                    return BusinessResult.Failure<Response>(new Error(nameof(template.EmailType), BusinessErrorMessage.InvalidEmailType));
            }

            return BusinessResult.Success(new Response(messageId, command.RecipientEmail));
        }
    }
}