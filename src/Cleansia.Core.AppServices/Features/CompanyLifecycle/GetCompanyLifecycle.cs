using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.CompanyLifecycle.DTOs;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.CompanyLifecycle;

/// <summary>
/// The admin's own company's lifecycle page (ADR-0064 D4): the state, every stamp with its actor,
/// and the live settlement facts with the date the archive becomes admissible.
/// </summary>
public class GetCompanyLifecycle
{
    public record Query : IQuery<CompanyLifecycleDto>;

    public class Handler(
        ITenantRepository tenantRepository,
        ITenantProvider tenantProvider,
        ICountryConfigurationRepository countryConfigurationRepository,
        ICompanySettlementReader settlementReader,
        IAppConfigurationProvider configurationProvider,
        IUserRepository userRepository) : IQueryHandler<Query, CompanyLifecycleDto>
    {
        public async Task<BusinessResult<CompanyLifecycleDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var tenantId = tenantProvider.GetCurrentTenantId();
            var tenant = tenantId is null ? null : await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is null)
            {
                return BusinessResult.Failure<CompanyLifecycleDto>(
                    new Error(DeactivateCompany.ErrorCode, BusinessErrorMessage.TenantNotFound));
            }

            var defaultMarket = await countryConfigurationRepository.GetDefaultMarketAsync(cancellationToken);
            var facts = await settlementReader.ReadAsync(cancellationToken);
            var horizonDays = await configurationProvider.GetAsync(TenantSettingCatalog.ChargebackHorizonDays, cancellationToken);

            return BusinessResult.Success(tenant.MapToDto(
                operatesDefaultMarket: defaultMarket?.OperatorTenantId == tenant.Id,
                facts,
                chargebackHorizonEndsOn: facts.LatestCardPaidCleaningDateTime?.AddDays(horizonDays),
                deactivatedByEmail: await EmailOfAsync(tenant.DeactivatedBy, cancellationToken),
                windDownRequestedByEmail: await EmailOfAsync(tenant.WindDownRequestedBy, cancellationToken),
                archiveRequestedByEmail: await EmailOfAsync(tenant.ArchiveRequestedBy, cancellationToken)));
        }

        private async Task<string?> EmailOfAsync(string? userId, CancellationToken cancellationToken) =>
            userId is null ? null : (await userRepository.GetByIdAsync(userId, cancellationToken))?.Email;
    }
}
