using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

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

            // Flush the delete HERE (not via the pipeline's later CommitAsync) so a reference inserted
            // after IsInUseAsync passed but before commit — the check-then-act TOCTOU window — surfaces its
            // ON DELETE RESTRICT violation where it can be mapped. The catalog FKs are Restrict, so the DB
            // is the final arbiter: a restrict/FK violation means a row now references this service, so
            // resolve to ServiceInUse instead of letting a raw DbUpdateException reach the pipeline as a
            // 500. On success the entity is Deleted-then-detached, so the pipeline's later commit is a safe
            // no-op.
            try
            {
                await serviceRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbConstraintViolation.IsForeignKeyViolation(ex))
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.ServiceId), BusinessErrorMessage.ServiceInUse));
            }

            return BusinessResult.Success(new Response(service!.Id));
        }
    }
}