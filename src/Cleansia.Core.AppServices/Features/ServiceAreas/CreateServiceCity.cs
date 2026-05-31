using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.ServiceAreas;

public class CreateServiceCity
{
    public record Command(string CountryId, string Name, string? ZipPrefix) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryRepository countryRepository, IServiceCityRepository cityRepository)
        {
            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await countryRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryNotFound);

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.ZipPrefix)
                .MaximumLength(20)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(async (cmd, ct) =>
                    !await cityRepository.ExistsWithNameInCountryAsync(cmd.CountryId, cmd.Name, excludeId: null, ct))
                .WithMessage(BusinessErrorMessage.ServiceCityAlreadyExists);
        }
    }

    internal class Handler(IServiceCityRepository cityRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var city = ServiceCity.Create(command.CountryId, command.Name, command.ZipPrefix);
            cityRepository.Add(city);
            await Task.CompletedTask;
            return BusinessResult.Success(new Response(city.Id));
        }
    }
}
