using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.InvoiceTemplates.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.InvoiceTemplates;

public class GetInvoiceTemplateById
{
    public record Query(string InvoiceTemplateId) : IQuery<InvoiceTemplateDetailDto>;

    public class Validator : AbstractValidator<Query>
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
        : IQueryHandler<Query, InvoiceTemplateDetailDto>
    {
        public async Task<BusinessResult<InvoiceTemplateDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var template = await invoiceTemplateRepository
                .GetQueryable()
                .Include(t => t.Country)
                .Include(t => t.Language)
                .FirstOrDefaultAsync(t => t.Id == query.InvoiceTemplateId, cancellationToken);

            return BusinessResult.Success(template!.MapToDetailDto());
        }
    }
}