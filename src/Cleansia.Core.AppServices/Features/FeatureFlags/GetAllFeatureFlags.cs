using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.FeatureFlags.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.FeatureFlags;

public static class GetAllFeatureFlags
{
    public record Query(string? Scope = null) : IQuery<List<FeatureFlagDto>>;

    internal class Handler(IFeatureFlagRepository featureFlagRepository) : IQueryHandler<Query, List<FeatureFlagDto>>
    {
        public async Task<BusinessResult<List<FeatureFlagDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = featureFlagRepository.GetAll();

            if (!string.IsNullOrEmpty(request.Scope))
                query = query.Where(f => f.Scope == request.Scope);

            var flags = await query
                .AsNoTracking()
                .OrderBy(f => f.Name)
                .ThenBy(f => f.Scope)
                .Select(f => new FeatureFlagDto(
                    f.Id, f.Name, f.Description, f.IsEnabled,
                    f.Scope, f.ScopeValue, f.CreatedOn, f.UpdatedOn))
                .ToListAsync(cancellationToken);

            return BusinessResult.Success(flags);
        }
    }
}
