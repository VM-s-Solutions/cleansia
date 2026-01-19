using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Packages;

public class DeletePackage
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

    internal class Handler(IPackageRepository packageRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var package = await packageRepository.GetByIdAsync(command.PackageId, cancellationToken);

            packageRepository.Remove(package!);

            return BusinessResult.Success(new Response(package!.Id));
        }
    }
}