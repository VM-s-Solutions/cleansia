using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.InvoiceTemplates;

public class ActivateInvoiceTemplate
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
                .WithMessage(BusinessErrorMessage.InvoiceTemplateNotFound);
        }
    }

    internal class Handler(IInvoiceTemplateRepository invoiceTemplateRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = await invoiceTemplateRepository.GetByIdAsync(command.InvoiceTemplateId, cancellationToken);

            // Deactivate any existing active template for same country/language
            var existingActive = await invoiceTemplateRepository
                .GetQueryable()
                .Where(t => t.CountryId == template!.CountryId
                    && t.LanguageId == template.LanguageId
                    && t.IsActive
                    && t.Id != template.Id)
                .ToListAsync(cancellationToken);

            foreach (var existingTemplate in existingActive)
            {
                existingTemplate.Deactivate();
            }

            template!.Activate();

            return BusinessResult.Success(new Response(true));
        }
    }
}