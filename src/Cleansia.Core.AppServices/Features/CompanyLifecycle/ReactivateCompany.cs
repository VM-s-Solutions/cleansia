using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.CompanyLifecycle;

/// <summary>
/// Reopens the admin's own company from <c>Deactivated</c> only (ADR-0064 D5): its markets are
/// listed again and its cleaners sign in again, and the wind-down stamps are cleared so a later
/// wind-down is announced afresh. What a wind-down already did — cancelled orders, cancelled Plus,
/// discharged credit — does not come back. A frozen or archived company is never reopened here.
/// </summary>
[AuditAction("company.reactivate", ResourceType = "Tenant")]
public class ReactivateCompany
{
    public record Command : ICommand<Response>;

    public record Response(CompanyLifecycleState State);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ITenantRepository tenantRepository, ITenantProvider tenantProvider)
        {
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(async (_, ct) => await CompanyAsync(ct) is { IsFrozen: false })
                .WithMessage(BusinessErrorMessage.CompanyArchived)
                .MustAsync(async (_, ct) => await CompanyAsync(ct) is { IsDeactivated: true })
                .WithMessage(BusinessErrorMessage.CompanyNotDeactivated)
                .OverridePropertyName(ErrorCode);

            async Task<Tenant?> CompanyAsync(CancellationToken ct)
            {
                var tenantId = tenantProvider.GetCurrentTenantId();
                return tenantId is null ? null : await tenantRepository.GetByIdAsync(tenantId, ct);
            }
        }
    }

    public const string ErrorCode = "Company";

    public class Handler(
        ITenantRepository tenantRepository,
        ITenantProvider tenantProvider,
        IAuditContext auditContext) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var tenantId = tenantProvider.GetCurrentTenantId();
            var tenant = tenantId is null ? null : await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is null)
            {
                return BusinessResult.Failure<Response>(new Error(ErrorCode, BusinessErrorMessage.TenantNotFound));
            }

            var before = CompanyLifecycleSnapshot.Of(tenant);
            tenant.Reactivate();
            auditContext.RecordChange("Tenant", tenant.Id, before, CompanyLifecycleSnapshot.Of(tenant));

            return BusinessResult.Success(new Response(tenant.State));
        }
    }
}
