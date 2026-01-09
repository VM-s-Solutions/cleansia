using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.ReceiptTemplates;

public class ActivateReceiptTemplate
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
                .WithMessage(BusinessErrorMessage.ReceiptTemplateNotFound);
        }
    }

    internal class Handler(IReceiptTemplateRepository receiptTemplateRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = await receiptTemplateRepository.GetByIdAsync(command.ReceiptTemplateId, cancellationToken);

            // Deactivate any existing active template for same country/language
            var existingActive = await receiptTemplateRepository
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