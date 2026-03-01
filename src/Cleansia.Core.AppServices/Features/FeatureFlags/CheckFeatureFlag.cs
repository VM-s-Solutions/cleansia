using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.FeatureFlags;

public static class CheckFeatureFlag
{
    public record Query(string FeatureName, string? CountryId = null, string? TenantId = null) : IQuery<Response>;

    internal class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.FeatureName).NotEmpty();
        }
    }

    internal class Handler(IAppConfigurationProvider configProvider) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var isEnabled = await configProvider.IsFeatureEnabledAsync(
                request.FeatureName, request.CountryId, request.TenantId, cancellationToken);
            return BusinessResult.Success(new Response(request.FeatureName, isEnabled));
        }
    }

    public record Response(string FeatureName, bool IsEnabled);
}
