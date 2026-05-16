using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class DeleteUserAccount
{
    public record Command : ICommand;

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
                deactivationReason: "GDPR_DELETION",
                resolveAuditActor: user => (user.Email, null),
                cancellationToken);
        }
    }
}
