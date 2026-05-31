using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.ServiceAreas;

public class UpdateServiceCity
{
    public record Command(string Id, string Name, string? ZipPrefix, bool IsActive) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IServiceCityRepository cityRepository)
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await cityRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.ServiceCityNotFound);

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.ZipPrefix)
                .MaximumLength(20)
                .WithMessage(BusinessErrorMessage.MaxLength);

            // Name-uniqueness within country — load the existing row to get
            // the country id, then check.
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(async (cmd, ct) =>
                {
                    var existing = await cityRepository.GetByIdAsync(cmd.Id, ct);
                    if (existing == null) return true; // earlier rule will catch
                    return !await cityRepository.ExistsWithNameInCountryAsync(
                        existing.CountryId, cmd.Name, excludeId: cmd.Id, ct);
                })
                .WithMessage(BusinessErrorMessage.ServiceCityAlreadyExists);
        }
    }

    internal class Handler(IServiceCityRepository cityRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var city = await cityRepository.GetByIdAsync(command.Id, cancellationToken);
            city!.Update(command.Name, command.ZipPrefix);
            city.IsActive = command.IsActive;
            return BusinessResult.Success(new Response(city.Id));
        }
    }
}
