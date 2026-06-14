using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class DeleteUserAccount
{
    public record Command : ICommand;

    // Required even though the command is parameterless: the validation pipeline rejects any *Command
    // with no registered validator. The customer self-delete operates on the session user (no input to
    // validate); eligibility (active user, no pending request, no blocking order/invoice) is enforced
    // inside the GDPR deletion service, which the handler delegates to.
    public class Validator : AbstractValidator<Command>;

    internal class Handler(
        IUserSessionProvider userSessionProvider,
        IGdprDeletionService gdprDeletionService)
        : ICommandHandler<Command>
    {
        public Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            // userId is non-null past the controller's [Permission] gate.
            var userId = userSessionProvider.GetUserId()!;
            return gdprDeletionService.DeleteUserAccountAsync(
                userId,
                deactivationReason: GdprAuditReasons.SelfDeletion,
                resolveAuditActor: user => (user.Email, null),
                cancellationToken);
        }
    }
}
