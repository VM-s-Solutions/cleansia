using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Extras;

public class DeleteExtra
{
    public record Command(string ExtraId) : ICommand<Response>;

    public record Response(string ExtraId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IExtraRepository extraRepository)
        {
            RuleFor(x => x.ExtraId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(extraRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ExtraNotFound)
                .MustAsync(async (id, ct) =>
                    !await extraRepository.IsInUseAsync(id, ct))
                .WithMessage(BusinessErrorMessage.ExtraInUse);
        }
    }

    public class Handler(IExtraRepository extraRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var isInUse = await extraRepository.IsInUseAsync(command.ExtraId, cancellationToken);
            if (isInUse)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.ExtraId), BusinessErrorMessage.ExtraInUse));
            }

            var extra = await extraRepository.GetByIdAsync(command.ExtraId, cancellationToken);
            if (extra is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ExtraId), BusinessErrorMessage.ExtraNotFound));
            }

            extraRepository.Remove(extra);

            // Flush HERE so a line inserted after IsInUseAsync passed surfaces FK_OrderExtras_Extras_ExtraId's
            // ON DELETE RESTRICT where it can be mapped to extra.in_use; ExtraPrices is Cascade, so a
            // priced-but-never-ordered extra deletes cleanly. The DB is the final arbiter of the
            // check-then-act window: a restrict/FK violation means a row now references this extra. On
            // success the entity is Deleted-then-detached, so the pipeline's later commit is a safe no-op.
            try
            {
                await extraRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbConstraintViolation.IsForeignKeyViolation(ex))
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.ExtraId), BusinessErrorMessage.ExtraInUse));
            }

            return BusinessResult.Success(new Response(extra.Id));
        }
    }
}
