using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Countries;

/// <summary>
/// Admin-only: toggles whether the company operates in this country. Drives
/// the customer/partner-facing pickers via <see cref="GetServicedCountries"/>.
/// </summary>
public class SetCountryServiced
{
    public record Command(string CountryId, bool IsServiced) : ICommand<Response>;

    public record Response(string Id, bool IsServiced);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryRepository countryRepository)
        {
            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await countryRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryNotFound);
        }
    }

    internal class Handler(ICountryRepository countryRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var country = await countryRepository.GetByIdAsync(command.CountryId, cancellationToken);
            country!.SetServiced(command.IsServiced);
            return BusinessResult.Success(new Response(country.Id, country.IsServiced));
        }
    }
}
