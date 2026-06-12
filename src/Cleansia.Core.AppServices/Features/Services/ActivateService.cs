using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Services;

/// <summary>
/// Reverses <see cref="DeactivateService"/>: the service reappears in the customer booking wizard.
/// The last DeactivatedBy/DeactivatedOn audit trail is intentionally kept. Idempotent.
/// </summary>
public class ActivateService
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
                .WithMessage(BusinessErrorMessage.ServiceNotFound);
        }
    }

    public class Handler(IServiceRepository serviceRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var service = await serviceRepository.GetByIdAsync(command.ServiceId, cancellationToken);

            service!.IsActive = true;

            return BusinessResult.Success(new Response(service.Id));
        }
    }
}
