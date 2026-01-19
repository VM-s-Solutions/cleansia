using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.InvoiceTemplates;

public class DeleteInvoiceTemplate
{
    public record Command(string InvoiceTemplateId) : ICommand<Response>;

    public record Response(bool Success);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IInvoiceTemplateRepository invoiceTemplateRepository)
        {
            RuleFor(x => x.InvoiceTemplateId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(invoiceTemplateRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceTemplateNotFound)
                .MustAsync(async (id, ct) =>
                {
                    var template = await invoiceTemplateRepository.GetByIdAsync(id, ct);
                    return template is not null && !template.IsActive;
                })
                .WithMessage(BusinessErrorMessage.CannotDeleteActiveTemplate);
        }
    }

    internal class Handler(IInvoiceTemplateRepository invoiceTemplateRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = await invoiceTemplateRepository.GetByIdAsync(command.InvoiceTemplateId, cancellationToken);

            invoiceTemplateRepository.Remove(template!);

            return BusinessResult.Success(new Response(true));
        }
    }
}