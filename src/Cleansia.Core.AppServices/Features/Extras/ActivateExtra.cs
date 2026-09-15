using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Extras;

/// <summary>
/// Reverses <see cref="DeactivateExtra"/>: the extra reappears in the customer booking wizard. The
/// last DeactivatedBy/DeactivatedOn audit trail is intentionally kept. Idempotent.
/// </summary>
public class ActivateExtra
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
                .WithMessage(BusinessErrorMessage.ExtraNotFound);
        }
    }

    public class Handler(IExtraRepository extraRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var extra = await extraRepository.GetByIdAsync(command.ExtraId, cancellationToken);
            if (extra is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ExtraId), BusinessErrorMessage.ExtraNotFound));
            }

            extra.IsActive = true;

            return BusinessResult.Success(new Response(extra.Id));
        }
    }
}
