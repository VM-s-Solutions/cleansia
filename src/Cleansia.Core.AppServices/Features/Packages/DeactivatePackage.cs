using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Packages;

/// <summary>
/// Soft-retire a package (ADR-0007): sets IsActive=false with the deactivating admin recorded via
/// the auditable domain primitive. The package disappears from the customer booking wizard
/// (GetPackageOverview filters IsActive) but stays referenceable — deactivating an in-use package
/// is allowed, unlike delete, because existing orders/carts keep their line items. Idempotent —
/// calling on an already-inactive package returns success without an error.
/// </summary>
public class DeactivatePackage
{
    public record Command(string PackageId) : ICommand<Response>;

    public record Response(string PackageId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IPackageRepository packageRepository)
        {
            RuleFor(x => x.PackageId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(packageRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PackageNotFound);
        }
    }

    public class Handler(
        IPackageRepository packageRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var package = await packageRepository.GetByIdAsync(command.PackageId, cancellationToken);

            if (!package!.IsActive)
            {
                return BusinessResult.Success(new Response(package.Id));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            package.Deactivated(actorId, DateTimeOffset.UtcNow);

            return BusinessResult.Success(new Response(package.Id));
        }
    }
}
