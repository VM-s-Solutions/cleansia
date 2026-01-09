using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Countries;

public class CreateCountry
{
    public record Command(
        string IsoCode,
        string Name) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryRepository countryRepository)
        {
            RuleFor(x => x.IsoCode)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(3)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .MustAsync(async (isoCode, ct) =>
                    !await countryRepository.ExistsWithIsoCodeAsync(isoCode, ct))
                .WithMessage(BusinessErrorMessage.CountryIsoCodeAlreadyExists);

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    internal class Handler(ICountryRepository countryRepository)
        : ICommandHandler<Command, Response>
    {
        public Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var country = Country.Create(command.Name, command.IsoCode);

            countryRepository.Add(country);

            return Task.FromResult(BusinessResult.Success(new Response(country.Id)));
        }
    }
}