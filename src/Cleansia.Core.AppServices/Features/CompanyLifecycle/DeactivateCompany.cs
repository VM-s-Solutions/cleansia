using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.CompanyLifecycle;

/// <summary>
/// Closes the door on the admin's own company (ADR-0064 D1): from the commit on, its markets are
/// gone from every "serviced" read and its cleaners are refused on the partner audiences. It cancels
/// nothing and e-mails nobody — the wind-down does that. The company is the ambient tenant; there is
/// no tenant id on the wire.
///
/// <para>Refused while the company holds the default market: every anonymous identity request on
/// every host is scoped to the default market's operator, so delisting it would refuse them all
/// until somebody runs SQL. The owner moves the flag with <c>SetDefaultMarket</c> first.</para>
///
/// <para>When a wind-down date is already set the sweep is run again from here, with the door now
/// closed: anything booked since the announcement is cancelled and refunded, credit is discharged and
/// the last pay period is closed. Deactivation never waits on a run already in flight.</para>
/// </summary>
[AuditAction("company.deactivate", ResourceType = "Tenant")]
public class DeactivateCompany
{
    public record Command : ICommand<Response>;

    public record Response(CompanyLifecycleState State);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            ITenantRepository tenantRepository,
            ITenantProvider tenantProvider,
            ICountryConfigurationRepository countryConfigurationRepository)
        {
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(async (_, ct) => await CompanyAsync(ct) is not null)
                .WithMessage(BusinessErrorMessage.TenantNotFound)
                .MustAsync(async (_, ct) => await CompanyAsync(ct) is { IsFrozen: false })
                .WithMessage(BusinessErrorMessage.CompanyArchived)
                .MustAsync(async (_, ct) => await CompanyAsync(ct) is { IsDeactivated: false })
                .WithMessage(BusinessErrorMessage.CompanyAlreadyDeactivated)
                .MustAsync(async (_, ct) => !await OperatesDefaultMarketAsync(ct))
                .WithMessage(BusinessErrorMessage.CompanyOperatesDefaultMarket)
                .OverridePropertyName(ErrorCode);

            async Task<Tenant?> CompanyAsync(CancellationToken ct)
            {
                var tenantId = tenantProvider.GetCurrentTenantId();
                return tenantId is null ? null : await tenantRepository.GetByIdAsync(tenantId, ct);
            }

            async Task<bool> OperatesDefaultMarketAsync(CancellationToken ct)
            {
                var defaultMarket = await countryConfigurationRepository.GetDefaultMarketAsync(ct);
                return defaultMarket?.OperatorTenantId is { } operatorTenantId
                    && operatorTenantId == tenantProvider.GetCurrentTenantId();
            }
        }
    }

    public const string ErrorCode = "Company";

    public class Handler(
        ITenantRepository tenantRepository,
        ITenantProvider tenantProvider,
        IUserSessionProvider userSessionProvider,
        IAuditContext auditContext,
        IPendingDispatch pendingDispatch,
        IOutboxMessageRepository outboxMessageRepository,
        TimeProvider timeProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var tenantId = tenantProvider.GetCurrentTenantId();
            var tenant = tenantId is null ? null : await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is null)
            {
                return BusinessResult.Failure<Response>(new Error(ErrorCode, BusinessErrorMessage.TenantNotFound));
            }

            var now = timeProvider.GetUtcNow();
            var before = CompanyLifecycleSnapshot.Of(tenant);
            tenant.Deactivate(userSessionProvider.GetUserId()!, now);
            if (tenant.IsWindDownRequested)
            {
                await CompanyWindDownDispatch.EnqueueAsync(pendingDispatch, outboxMessageRepository, tenant.Id, now, cancellationToken);
            }

            auditContext.RecordChange("Tenant", tenant.Id, before, CompanyLifecycleSnapshot.Of(tenant));

            return BusinessResult.Success(new Response(tenant.State));
        }
    }
}
