using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Currencies;

public class GetCurrencyById
{
    public record Query(string CurrencyId) : IQuery<CurrencyDetailDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.CurrencyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) =>
                    await currencyRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CurrencyNotFound);
        }
    }

    internal class Handler(ICurrencyRepository currencyRepository)
        : IQueryHandler<Query, CurrencyDetailDto>
    {
        public async Task<BusinessResult<CurrencyDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var currency = await currencyRepository.GetByIdAsync(query.CurrencyId, cancellationToken);

            if (currency is null)
            {
                return BusinessResult.Failure<CurrencyDetailDto>(new Error(nameof(query.CurrencyId), BusinessErrorMessage.CurrencyNotFound));
            }

            return BusinessResult.Success(currency.MapToDetailDto());
        }
    }
}