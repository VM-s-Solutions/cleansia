using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public class GetAllGdprRequests
{
    public class Request : DataRangeRequest, IRequest<PagedData<GdprRequestDto>>;

    internal class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            // Defense-in-depth against an admin / compromised admin sending a huge limit to dump
            // the whole audit table. Tighter than DataRangeRequest's default range for this surface.
            RuleFor(q => q.Limit).InclusiveBetween(1, 100);
        }
    }

    internal class Handler(IGdprRequestRepository gdprRequestRepository)
        : IRequestHandler<Request, PagedData<GdprRequestDto>>
    {
        public async Task<PagedData<GdprRequestDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var sort = request.Sort?.MapToDomain().ToList();
            if (sort is null || sort.Count == 0)
            {
                // Article-30 surface default: newest first. Without this, an empty Sort would leave
                // the window unordered (Skip/Take on a heap) — the bug this re-shape fixes was the
                // sort being applied AFTER paging, returning the wrong rows entirely.
                sort = [new SortDefinition { Field = nameof(GdprRequest.CreatedOn), Direction = SortDirection.Descending }];
            }

            var totalItems = await gdprRequestRepository.GetCountAsync(null, cancellationToken);

            var items = await gdprRequestRepository
                .GetPagedSort<GdprRequestSort>(request.Offset, request.Limit, null, sort)
                .AsNoTracking()
                .Select(r => r.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}
