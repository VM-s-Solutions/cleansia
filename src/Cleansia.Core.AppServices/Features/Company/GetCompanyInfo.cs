using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Company.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Company;

public class GetCompanyInfo
{
    public record Query() : IQuery<CompanyInfoDetailDto?>;

    internal class Handler(
        ICompanyInfoRepository companyInfoRepository,
        ICountryRepository countryRepository)
        : IQueryHandler<Query, CompanyInfoDetailDto?>
    {
        public async Task<BusinessResult<CompanyInfoDetailDto?>> Handle(Query query, CancellationToken cancellationToken)
        {
            var companyInfo = await companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);

            if (companyInfo is null)
            {
                return BusinessResult.Success<CompanyInfoDetailDto?>(null);
            }

            var country = await countryRepository.GetByIdAsync(companyInfo.CountryId, cancellationToken);

            return BusinessResult.Success<CompanyInfoDetailDto?>(companyInfo.MapToDetailDto(country));
        }
    }
}