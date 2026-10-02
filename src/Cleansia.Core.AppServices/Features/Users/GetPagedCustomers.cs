#nullable enable
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.AppServices.Features.Users.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Users;

public class GetPagedCustomers
{
    public class Request : DataRangeRequest, IRequest<PagedData<AdminCustomerListItem>>
    {
        public CustomerFilter? Filter { get; init; }
    }

    internal class Handler(IUserRepository userRepository)
        : IRequestHandler<Request, PagedData<AdminCustomerListItem>>
    {
        public async Task<PagedData<AdminCustomerListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = UserSpecification.Create(
                userProfiles: [(int)UserProfile.Customer],
                isActive: request.Filter?.IsActive,
                searchTerm: request.Filter?.SearchTerm);

            var filter = specification.SatisfiedBy();

            var totalItems = await userRepository.GetCountAsync(filter, cancellationToken);
            var items = await userRepository
                .GetPagedSort<UserSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .Select(user => user.MapToAdminCustomerListItem())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}
