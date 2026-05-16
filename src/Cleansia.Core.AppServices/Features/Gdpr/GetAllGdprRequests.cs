using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class GetAllGdprRequests
{
    public record Query(int Page = 1, int PageSize = 20) : IQuery<List<GdprRequestDto>>;

    internal class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            // Defense-in-depth against an admin / compromised admin sending
            // pageSize=int.MaxValue to dump the whole audit table.
            RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
            RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        }
    }

    internal class Handler(IGdprRequestRepository gdprRequestRepository)
        : IQueryHandler<Query, List<GdprRequestDto>>
    {
        public async Task<BusinessResult<List<GdprRequestDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var offset = (request.Page - 1) * request.PageSize;

            var requests = await gdprRequestRepository.GetPaged(offset, request.PageSize)
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedOn)
                .Select(r => new GdprRequestDto(
                    r.Id, r.UserId, r.RequestType, r.Status,
                    r.ProcessedBy, r.CompletedAt, r.Notes, r.CreatedOn))
                .ToListAsync(cancellationToken);

            return BusinessResult.Success(requests);
        }
    }
}
