using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.AdminUsers;

public class DeactivateAdminUser
{
    public record Command(string UserId, string CurrentUserId) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (userId, ct) =>
                    await userRepository.GetAll()
                        .AnyAsync(u => u.Id == userId && u.Profile == UserProfile.Administrator, ct))
                .WithMessage(BusinessErrorMessage.AdminUserNotFound);

            RuleFor(x => x)
                .Must(x => x.UserId != x.CurrentUserId)
                .WithMessage(BusinessErrorMessage.CannotDeactivateSelf);
        }
    }

    internal class Handler(IUserRepository userRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository
                .GetAll()
                .FirstOrDefaultAsync(
                    u => u.Id == command.UserId && u.Profile == UserProfile.Administrator,
                    cancellationToken);

            user!.Deactivated(command.CurrentUserId, DateTimeOffset.UtcNow);

            return BusinessResult.Success(new Response(user.Id));
        }
    }
}