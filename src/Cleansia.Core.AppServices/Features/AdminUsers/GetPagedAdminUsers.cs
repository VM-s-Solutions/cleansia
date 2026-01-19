using Cleansia.Core.AppServices.Features.AdminUsers.DTOs;
using Cleansia.Core.AppServices.Features.AdminUsers.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.AdminUsers;

public class GetPagedAdminUsers
{
    public class Request : DataRangeRequest, IRequest<PagedData<AdminUserListItem>>
    {
        public AdminUserFilter? Filter { get; init; }
    }

    internal class Handler(IUserRepository userRepository)
        : IRequestHandler<Request, PagedData<AdminUserListItem>>
    {
        public async Task<PagedData<AdminUserListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = UserSpecification.Create(
                userProfiles: [(int)UserProfile.Administrator],
                isActive: request.Filter?.IsActive,
                searchTerm: request.Filter?.SearchTerm);

            var filter = specification.SatisfiedBy();

            var totalItems = await userRepository.GetCountAsync(filter, cancellationToken);
            var items = await userRepository
                .GetPagedSort<UserSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .Select(user => user.MapToAdminListItem()!)
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}