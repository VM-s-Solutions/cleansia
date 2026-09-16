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
            string? ownerId = null;
            if (role != UserProfile.Administrator.ToString())
            {
                ownerId = userSessionProvider.GetUserId() ?? string.Empty;
                filterDto = filterDto is null
                    ? new DisputeFilter(null, ownerId, null, null, null, null, null, null, null, null, null, null)
                    : filterDto with { UserId = ownerId, CustomerEmail = null, CustomerName = null };
            }

            var specification = filterDto.MapToDomain();
            var filter = specification.SatisfiedBy();

            // A customer's disputes sit in the books of every company they booked with (a dispute carries
            // its order's operator), so their list reads across companies pinned by their own id; an
            // admin's list stays their company's through the filter.
            var totalItems = ownerId is null
                ? await disputeRepository.GetCountAsync(filter, cancellationToken)
                : await disputeRepository.GetCountForOwnerAsync(ownerId, filter, cancellationToken);
            var page = ownerId is null
                ? disputeRepository.GetPagedSort<DisputeSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                : disputeRepository.GetPagedSortForOwner<DisputeSort>(ownerId, request.Offset, request.Limit, filter, request.Sort.MapToDomain());
            // Establish the page under the access filter before loading its cross-company customer navigation.
            var ids = await page.Select(d => d.Id).ToListAsync(cancellationToken);
            var rows = await disputeRepository.GetQueryableIgnoringTenant()
                .Where(d => ids.Contains(d.Id))
                .Include(d => d.Order)
                .Include(d => d.User)
                .AsNoTracking()
                .ToDictionaryAsync(d => d.Id, cancellationToken);
            var items = ids.Where(rows.ContainsKey).Select(id => rows[id].MapToListItem()).ToList();

            return items.MapToDto(totalItems, request);
        }
    }
}
