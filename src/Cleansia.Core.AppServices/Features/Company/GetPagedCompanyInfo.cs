using Cleansia.Core.AppServices.Features.Company.DTOs;
using Cleansia.Core.AppServices.Features.Company.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Company;

public class GetPagedCompanyInfo
{
    public class Request : DataRangeRequest, IRequest<PagedData<CompanyInfoListItem>>
    {
        public CompanyInfoFilter? Filter { get; init; }
    }

    internal class Handler(ICompanyInfoRepository companyInfoRepository)
        : IRequestHandler<Request, PagedData<CompanyInfoListItem>>
    {
        public async Task<PagedData<CompanyInfoListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = CompanyInfoSpecification.Create(
                searchTerm: request.Filter?.SearchTerm,
                countryId: request.Filter?.CountryId,
                isActive: true
            );

            var filter = specification.SatisfiedBy();

            var totalItems = await companyInfoRepository.GetCountAsync(filter, cancellationToken);
            var items = await companyInfoRepository
                .GetPagedSort<CompanyInfoSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(c => c.Country)
                .AsNoTracking()
                .Select(companyInfo => companyInfo.MapToListItem())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}