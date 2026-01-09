using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Countries;

public class GetCountryById
{
    public record Query(string CountryId) : IQuery<CountryDetailDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(ICountryRepository countryRepository)
        {
            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) =>
                    await countryRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryNotFound);
        }
    }

    internal class Handler(ICountryRepository countryRepository)
        : IQueryHandler<Query, CountryDetailDto>
    {
        public async Task<BusinessResult<CountryDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var country = await countryRepository.GetByIdAsync(query.CountryId, cancellationToken);

            if (country is null)
            {
                return BusinessResult.Failure<CountryDetailDto>(new Error(nameof(query.CountryId), BusinessErrorMessage.CountryNotFound));
            }

            return BusinessResult.Success(country.MapToDetailDto());
        }
    }
}