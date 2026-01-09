#nullable enable
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.AppServices.Features.Users.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Users;

public class GetPagedUsers
{
    public class Request : DataRangeRequest, IRequest<PagedData<UserListItem>>
    {
        public UserFilter? Filter { get; init; }
    }

    internal class Handler(
        IUserRepository userRepository)
        : IRequestHandler<Request, PagedData<UserListItem>>
    {
        public async Task<PagedData<UserListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = UserSpecification.Create(
                id: request.Filter?.Id,
                isActive: request.Filter?.IsActive,
                firstName: request.Filter?.FirstName,
                lastName: request.Filter?.LastName,
                phoneNumber: request.Filter?.PhoneNumber,
                email: request.Filter?.Email,
                userProfiles: request.Filter?.UserProfiles,
                authenticationTypes: request.Filter?.AuthenticationTypes);

            var filter = specification.SatisfiedBy();

            var totalItems = await userRepository.GetCountAsync(filter, cancellationToken);
            var items = await userRepository
                .GetPagedSort<UserSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .Select(user => user.MapToDto()!)
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}