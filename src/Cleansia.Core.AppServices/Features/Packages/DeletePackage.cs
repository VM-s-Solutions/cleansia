using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

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
                .WithMessage(BusinessErrorMessage.PackageNotFound)
                .MustAsync(async (id, ct) =>
                    !await packageRepository.IsInUseAsync(id, ct))
                .WithMessage(BusinessErrorMessage.PackageInUse);
        }
    }

    public class Handler(IPackageRepository packageRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var isInUse = await packageRepository.IsInUseAsync(command.PackageId, cancellationToken);
            if (isInUse)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.PackageId), BusinessErrorMessage.PackageInUse));
            }

            var package = await packageRepository.GetByIdAsync(command.PackageId, cancellationToken);

            packageRepository.Remove(package!);

            // Flush the delete HERE (not via the pipeline's later CommitAsync) so a reference inserted
            // after IsInUseAsync passed but before commit — the check-then-act TOCTOU window — surfaces its
            // ON DELETE RESTRICT violation where it can be mapped. The catalog FKs are Restrict, so the DB
            // is the final arbiter: a restrict/FK violation means a row now references this package, so
            // resolve to PackageInUse instead of letting a raw DbUpdateException reach the pipeline as a
            // 500. On success the entity is Deleted-then-detached, so the pipeline's later commit is a safe
            // no-op.
            try
            {
                await packageRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbConstraintViolation.IsForeignKeyViolation(ex))
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.PackageId), BusinessErrorMessage.PackageInUse));
            }

            return BusinessResult.Success(new Response(package!.Id));
        }
    }
}