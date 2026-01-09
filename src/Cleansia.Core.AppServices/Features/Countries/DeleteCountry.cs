using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Countries;

public class DeleteCountry
{
    public record Command(string CountryId) : ICommand<Response>;

    public record Response(bool Success);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryRepository countryRepository)
        {
            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) =>
                    await countryRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryNotFound)
                .MustAsync(async (id, ct) =>
                    !await countryRepository.IsInUseAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryInUse);
        }
    }

    internal class Handler(ICountryRepository countryRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var country = await countryRepository.GetByIdAsync(command.CountryId, cancellationToken);

            if (country is null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.CountryId), BusinessErrorMessage.CountryNotFound));
            }

            // Double-check in handler as well for safety
            var isInUse = await countryRepository.IsInUseAsync(command.CountryId, cancellationToken);
            if (isInUse)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.CountryId), BusinessErrorMessage.CountryInUse));
            }

            countryRepository.Remove(country);

            return BusinessResult.Success(new Response(true));
        }
    }
}