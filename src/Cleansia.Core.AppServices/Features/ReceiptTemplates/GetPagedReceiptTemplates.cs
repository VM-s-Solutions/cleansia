using Cleansia.Core.AppServices.Features.ReceiptTemplates.DTOs;
using Cleansia.Core.AppServices.Features.ReceiptTemplates.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.ReceiptTemplates;

public class GetPagedReceiptTemplates
{
    public class Request : DataRangeRequest, IRequest<PagedData<ReceiptTemplateListItem>>
    {
        public ReceiptTemplateFilter? Filter { get; init; }
    }

    internal class Handler(IReceiptTemplateRepository receiptTemplateRepository)
        : IRequestHandler<Request, PagedData<ReceiptTemplateListItem>>
    {
        public async Task<PagedData<ReceiptTemplateListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = ReceiptTemplateSpecification.Create(
                searchTerm: request.Filter?.SearchTerm,
                countryId: request.Filter?.CountryId,
                languageId: request.Filter?.LanguageId,
                isActive: request.Filter?.IsActive
            );

            var filter = specification.SatisfiedBy();

            var totalItems = await receiptTemplateRepository.GetCountAsync(filter, cancellationToken);
            var items = await receiptTemplateRepository
                .GetPagedSort<ReceiptTemplateSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(t => t.Country)
                .Include(t => t.Language)
                .AsNoTracking()
                .Select(template => template.MapToListItem())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}