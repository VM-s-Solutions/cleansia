using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmailTemplates;

public class UpdateEmailTemplate
{
    public record Command(
        string EmailTemplateId,
        string Value) : ICommand<Response>;

    public record Response(string EmailTemplateId);

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

            RuleFor(x => x.Value)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(5000)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    internal class Handler(IEmailTemplateTranslationRepository emailTemplateRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = await emailTemplateRepository.GetByIdAsync(command.EmailTemplateId, cancellationToken);

            template!.UpdateValue(command.Value);

            return BusinessResult.Success(new Response(template.Id));
        }
    }
}