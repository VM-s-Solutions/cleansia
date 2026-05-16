using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;


namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class AdminDeleteUserAccount
{
    public record Command(string UserId) : ICommand;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(c => c.UserId)
                .NotEmpty()
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithId);
        }
    }

    internal class Handler(
        IUserSessionProvider userSessionProvider,
        IGdprDeletionService gdprDeletionService)
        : ICommandHandler<Command>
    {
        public Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var adminEmail = userSessionProvider.GetUserEmail() ?? "admin";

            return gdprDeletionService.DeleteUserAccountAsync(
                request.UserId,
                deactivationReason: "GDPR_ADMIN_DELETION",
                resolveAuditActor: _ => (adminEmail, $"Admin deletion by {adminEmail}"),
                cancellationToken);
        }
    }
}
