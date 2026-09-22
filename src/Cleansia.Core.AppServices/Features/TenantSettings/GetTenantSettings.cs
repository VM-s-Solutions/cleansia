using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.TenantSettings.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.TenantSettings;

/// <summary>
/// Every catalogue key with what the admin's own company has made of it: the default, the value in
/// effect and whether a row exists. The page is the catalogue, not the table — a key nothing reads any
/// more is not listed even if a row for it survives.
/// </summary>
public class GetTenantSettings
{
    public record Query : IQuery<Response>;

    public record Response(IReadOnlyList<TenantSettingDto> Settings);

    public class Handler(ITenantConfigurationRepository tenantConfigurationRepository)
        : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var rows = await tenantConfigurationRepository.GetAllAsync(cancellationToken);
            var storedByKey = rows.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);

            var settings = TenantSettingCatalog.All
                .Select(definition => definition.MapToDto(storedByKey.GetValueOrDefault(definition.Key)))
                .ToList();

            return BusinessResult.Success(new Response(settings));
        }
    }
}
