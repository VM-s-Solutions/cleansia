using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Extras;

/// <summary>
/// Soft-retire an extra: GetExtraOverview filters IsActive, so it leaves the booking wizard while every
/// OrderExtra that references it stays valid. This is the ONLY way to take a sold extra off sale --
/// DeleteExtra is refused once any order line references it. Idempotent.
/// </summary>
public class DeactivateExtra
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

    public class Handler(
        IExtraRepository extraRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var extra = await extraRepository.GetByIdAsync(command.ExtraId, cancellationToken);
            if (extra is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ExtraId), BusinessErrorMessage.ExtraNotFound));
            }

            if (!extra.IsActive)
            {
                return BusinessResult.Success(new Response(extra.Id));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            extra.Deactivated(actorId, DateTimeOffset.UtcNow);

            return BusinessResult.Success(new Response(extra.Id));
        }
    }
}
