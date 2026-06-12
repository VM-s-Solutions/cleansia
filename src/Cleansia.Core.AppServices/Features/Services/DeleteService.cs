using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Services;

public class DeleteService
{
    public record Command(string ServiceId) : ICommand<Response>;

    public record Response(string ServiceId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IServiceRepository serviceRepository)
        {
            RuleFor(x => x.ServiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(serviceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ServiceNotFound)
                .MustAsync(async (id, ct) =>
                    !await serviceRepository.IsInUseAsync(id, ct))
                .WithMessage(BusinessErrorMessage.ServiceInUse);
        }
    }

    public class Handler(IServiceRepository serviceRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var isInUse = await serviceRepository.IsInUseAsync(command.ServiceId, cancellationToken);
            if (isInUse)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.ServiceId), BusinessErrorMessage.ServiceInUse));
            }

            var service = await serviceRepository.GetByIdAsync(command.ServiceId, cancellationToken);

            serviceRepository.Remove(service!);

            return BusinessResult.Success(new Response(service!.Id));
        }
    }
}