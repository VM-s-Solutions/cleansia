using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Company.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Company;

public class GetCompanyInfoById
{
    public record Query(string CompanyInfoId) : IQuery<CompanyInfoDetailDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(ICompanyInfoRepository companyInfoRepository)
        {
            RuleFor(x => x.CompanyInfoId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(companyInfoRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.CompanyInfoNotFound);
        }
    }

    internal class Handler(ICompanyInfoRepository companyInfoRepository)
        : IQueryHandler<Query, CompanyInfoDetailDto>
    {
        public async Task<BusinessResult<CompanyInfoDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var companyInfo = await companyInfoRepository
                .GetQueryable()
                .Include(c => c.Country)
                .FirstOrDefaultAsync(c => c.Id == query.CompanyInfoId, cancellationToken);

            if (companyInfo is null)
            {
                return BusinessResult.Failure<CompanyInfoDetailDto>(
                    new Error(nameof(query.CompanyInfoId), BusinessErrorMessage.CompanyInfoNotFound));
            }

            return BusinessResult.Success(companyInfo.MapToDetailDto());
        }
    }
}