using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.ReceiptTemplates.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.ReceiptTemplates;

public class GetReceiptTemplateById
{
    public record Query(string ReceiptTemplateId) : IQuery<ReceiptTemplateDetailDto>;

    public class Validator : AbstractValidator<Query>
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
        : IQueryHandler<Query, ReceiptTemplateDetailDto>
    {
        public async Task<BusinessResult<ReceiptTemplateDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var template = await receiptTemplateRepository
                .GetQueryable()
                .Include(t => t.Country)
                .Include(t => t.Language)
                .FirstOrDefaultAsync(t => t.Id == query.ReceiptTemplateId, cancellationToken);

            return BusinessResult.Success(template!.MapToDetailDto());
        }
    }
}