using Cleansia.Core.AppServices.Features.InvoiceTemplates.DTOs;
using Cleansia.Core.AppServices.Features.InvoiceTemplates.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.InvoiceTemplates;

public class GetPagedInvoiceTemplates
{
    public class Request : DataRangeRequest, IRequest<PagedData<InvoiceTemplateListItem>>
    {
        public InvoiceTemplateFilter? Filter { get; init; }
    }

    internal class Handler(IInvoiceTemplateRepository invoiceTemplateRepository)
        : IRequestHandler<Request, PagedData<InvoiceTemplateListItem>>
    {
        public async Task<PagedData<InvoiceTemplateListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = InvoiceTemplateSpecification.Create(
                searchTerm: request.Filter?.SearchTerm,
                countryId: request.Filter?.CountryId,
                languageId: request.Filter?.LanguageId,
                isActive: request.Filter?.IsActive
            );

            var filter = specification.SatisfiedBy();

            var totalItems = await invoiceTemplateRepository.GetCountAsync(filter, cancellationToken);
            var items = await invoiceTemplateRepository
                .GetPagedSort<InvoiceTemplateSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(t => t.Country)
                .Include(t => t.Language)
                .AsNoTracking()
                .Select(template => template.MapToListItem())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}