using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.ReceiptTemplates;

public class DeleteReceiptTemplate
{
    public record Command(string ReceiptTemplateId) : ICommand<Response>;

    public record Response(bool Success);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IReceiptTemplateRepository receiptTemplateRepository)
        {
            RuleFor(x => x.ReceiptTemplateId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(receiptTemplateRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ReceiptTemplateNotFound)
                .MustAsync(async (id, ct) =>
                {
                    var template = await receiptTemplateRepository.GetByIdAsync(id, ct);
                    return template is not null && !template.IsActive;
                })
                .WithMessage(BusinessErrorMessage.CannotDeleteActiveTemplate);
        }
    }

    internal class Handler(IReceiptTemplateRepository receiptTemplateRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = await receiptTemplateRepository.GetByIdAsync(command.ReceiptTemplateId, cancellationToken);

            receiptTemplateRepository.Remove(template!);

            return BusinessResult.Success(new Response(true));
        }
    }
}