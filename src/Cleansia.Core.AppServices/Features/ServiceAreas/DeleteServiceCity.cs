using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.ServiceAreas;

public class DeleteServiceCity
{
    public record Command(string Id) : ICommand<Response>;

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
        }
    }

    internal class Handler(IServiceCityRepository cityRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var city = await cityRepository.GetByIdAsync(command.Id, cancellationToken);
            cityRepository.Remove(city!);
            return BusinessResult.Success(new Response(command.Id));
        }
    }
}
