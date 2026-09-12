using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Countries;

/// <summary>
/// Admin-only: toggles whether the company operates in this country. Drives
/// the customer/partner-facing pickers via <see cref="GetServicedCountries"/>.
///
/// <para>Switching a country ON is gated: <c>Country/GetServiced</c> feeds the wizard's address step
/// on every client, and a serviced country whose configuration is missing or whose currency is not
/// switched on is an address the quote cannot price — the customer meets the dead end three screens
/// after the admin created it. The gate moves that refusal to the admin. Switching OFF is never
/// gated.</para>
/// </summary>
public class SetCountryServiced
{
    public record Command(string CountryId, bool IsServiced) : ICommand<Response>;

    public record Response(string Id, bool IsServiced);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            ICountryRepository countryRepository,
            ICountryConfigurationRepository countryConfigurationRepository,
            ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await countryRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryNotFound)
                .MustAsync(async (id, ct) => await MarketIsReadyAsync(id, ct))
                .When(x => x.IsServiced, ApplyConditionTo.CurrentValidator)
                .WithMessage(BusinessErrorMessage.CountryMarketNotReady);

            async Task<bool> MarketIsReadyAsync(string countryId, CancellationToken ct)
            {
                var configuration = await countryConfigurationRepository.GetByCountryIdAsync(countryId, ct);
                if (string.IsNullOrWhiteSpace(configuration?.DefaultCurrencyCode))
                {
                    return false;
                }

                var currency = await currencyRepository.GetByCodeAsync(configuration.DefaultCurrencyCode, ct);
                return currency is { IsActive: true };
            }
        }
    }

    internal class Handler(ICountryRepository countryRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var country = await countryRepository.GetByIdAsync(command.CountryId, cancellationToken);
            if (country is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CountryId), BusinessErrorMessage.CountryNotFound));
            }

            country.SetServiced(command.IsServiced);
            return BusinessResult.Success(new Response(country.Id, country.IsServiced));
        }
    }
}
