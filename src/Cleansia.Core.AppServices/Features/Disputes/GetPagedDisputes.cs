#nullable enable
using System.Security.Claims;
using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Features.Disputes.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class GetPagedDisputes
{
    public class Request : DataRangeRequest, IRequest<PagedData<DisputeListItem>>
    {
        public DisputeFilter? Filter { get; init; }
    }

    internal class Handler(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider)
        : IRequestHandler<Request, PagedData<DisputeListItem>>
    {
        public async Task<PagedData<DisputeListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            var filterDto = request.Filter;
            if (role != UserProfile.Administrator.ToString())
            {
                var userId = userSessionProvider.GetUserId() ?? string.Empty;
                filterDto = filterDto is null
                    ? new DisputeFilter(null, userId, null, null, null, null, null, null, null, null, null, null)
                    : filterDto with { UserId = userId, CustomerEmail = null, CustomerName = null };
            }

            var specification = filterDto.MapToDomain();
            var filter = specification.SatisfiedBy();

            var totalItems = await disputeRepository.GetCountAsync(filter, cancellationToken);
            var items = await disputeRepository
                .GetPagedSort<DisputeSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(d => d.Order)
                .Include(d => d.User)
                .AsNoTracking()
                .Select(dispute => dispute.MapToListItem())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}
