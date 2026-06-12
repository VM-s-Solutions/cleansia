using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Services;

/// <summary>
/// Soft-retire a service (ADR-0007): sets IsActive=false with the deactivating admin recorded via
/// the auditable domain primitive. The service disappears from the customer booking wizard
/// (GetServiceOverview filters IsActive) but stays referenceable — deactivating an in-use service
/// is allowed, unlike delete, because existing orders/carts keep their line items. Idempotent —
/// calling on an already-inactive service returns success without an error.
/// </summary>
public class DeactivateService
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

    public class Handler(
        IServiceRepository serviceRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var service = await serviceRepository.GetByIdAsync(command.ServiceId, cancellationToken);

            if (!service!.IsActive)
            {
                return BusinessResult.Success(new Response(service.Id));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            service.Deactivated(actorId, DateTimeOffset.UtcNow);

            return BusinessResult.Success(new Response(service.Id));
        }
    }
}
