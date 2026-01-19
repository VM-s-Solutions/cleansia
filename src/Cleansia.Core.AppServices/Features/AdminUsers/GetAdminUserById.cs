using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminUsers.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.AdminUsers;

public class GetAdminUserById
{
    public record Query(string UserId) : IQuery<AdminUserDetailDto>;

    public class Validator : AbstractValidator<Query>
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
        }
    }

    internal class Handler(IUserRepository userRepository)
        : IQueryHandler<Query, AdminUserDetailDto>
    {
        public async Task<BusinessResult<AdminUserDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var user = await userRepository
                .GetAll()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Id == query.UserId && u.Profile == UserProfile.Administrator,
                    cancellationToken);

            return BusinessResult.Success(user!.MapToAdminDetailDto()!);
        }
    }
}