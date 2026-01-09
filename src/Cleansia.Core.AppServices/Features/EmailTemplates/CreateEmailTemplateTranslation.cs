using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmailTemplates;

public class CreateEmailTemplateTranslation
{
    public record Command(
        EmailType EmailType,
        string LanguageId,
        string Key,
        string Value) : ICommand<Response>;

    public record Response(string EmailTemplateId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IEmailTemplateTranslationRepository emailTemplateRepository,
            ILanguageRepository languageRepository)
        {
            RuleFor(x => x.EmailType)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEmailType);

            RuleFor(x => x.LanguageId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(languageRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.LanguageNotFound);

            RuleFor(x => x.Key)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Value)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(5000)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x)
                .MustAsync(async (cmd, cancellationToken) =>
                {
                    var exists = await emailTemplateRepository.GetAll()
                        .AnyAsync(e => e.EmailType == cmd.EmailType &&
                                       e.LanguageId == cmd.LanguageId &&
                                       e.Key == cmd.Key, cancellationToken);
                    return !exists;
                })
                .WithMessage(BusinessErrorMessage.EmailTemplateKeyExists);
        }
    }

    internal class Handler(IEmailTemplateTranslationRepository emailTemplateRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = EmailTemplateTranslation.Create(
                command.Key,
                command.Value,
                command.EmailType,
                command.LanguageId);

            emailTemplateRepository.Add(template);

            return BusinessResult.Success(new Response(template.Id));
        }
    }
}