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
/// Announces the admin's own company's last day of service and starts the sweep that honours it
/// (ADR-0064 D2): every customer and cleaner is told, bookings on or after the date are cancelled and
/// refunded in full, templates are paused, every Plus ends at period end, and — once the door is
/// closed — credit is discharged and the last pay period is invoiced. The date is set once; a request
/// without a date is a re-run of the sweep, which converges on whatever is left.
///
/// <para>The sweep is a queue consumer, not this request: a hundred paid bookings after the date are a
/// hundred Stripe calls, and each one must be committed before the next.</para>
/// </summary>
[AuditAction("company.wind_down", ResourceType = "Tenant")]
public class WindDownCompany
{
    public record Command(DateOnly? FromDate) : ICommand<Response>;

    public record Response(CompanyLifecycleState State, DateOnly? WindDownFrom);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            ITenantRepository tenantRepository,
            ITenantProvider tenantProvider,
            ICountryConfigurationRepository countryConfigurationRepository,
            TimeProvider timeProvider)
        {
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(async (_, ct) => await CompanyAsync(ct) is { IsFrozen: false })
                .WithMessage(BusinessErrorMessage.CompanyArchived)
                .MustAsync(async (command, ct) => command.FromDate is not null || !(await CompanyAsync(ct))!.IsWindDownRunning(timeProvider.GetUtcNow()))
                .WithMessage(BusinessErrorMessage.CompanyWindDownInProgress)
                .OverridePropertyName(ErrorCode);

            RuleFor(x => x.FromDate)
                .Cascade(CascadeMode.Stop)
                .MustAsync(async (fromDate, ct) => fromDate is null || await CompanyAsync(ct) is { IsWindDownRequested: false })
                .WithMessage(BusinessErrorMessage.CompanyWindDownAlreadyRequested)
                .MustAsync(async (fromDate, ct) => fromDate is not null || await CompanyAsync(ct) is { IsWindDownRequested: true })
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (fromDate, ct) => fromDate is null || !await IsPastInEarliestMarketAsync(fromDate.Value, ct))
                .WithMessage(BusinessErrorMessage.CompanyWindDownDateInPast)
                .WhenAsync(async (_, ct) => await CompanyAsync(ct) is { IsFrozen: false });

            async Task<Tenant?> CompanyAsync(CancellationToken ct)
            {
                var tenantId = tenantProvider.GetCurrentTenantId();
                return tenantId is null ? null : await tenantRepository.GetByIdAsync(tenantId, ct);
            }

            // "Today" is the latest local date among the company's markets, so a date that has
            // already passed in its easternmost market is refused even while it is still today
            // further west; a company with no market is read in UTC.
            async Task<bool> IsPastInEarliestMarketAsync(DateOnly fromDate, CancellationToken ct)
            {
                var now = timeProvider.GetUtcNow();
                var markets = await countryConfigurationRepository.GetOperatedByAsync(tenantProvider.GetCurrentTenantId()!, ct);
                var today = markets.Count == 0
                    ? WindDownCutoff.LocalDate(now, timeZoneId: null)
                    : markets.Max(m => WindDownCutoff.LocalDate(now, m.TimeZoneId));
                return fromDate < today;
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
            if (command.FromDate is { } fromDate)
            {
                tenant.RequestWindDown(fromDate, userSessionProvider.GetUserId()!, now);
            }

            await CompanyWindDownDispatch.EnqueueAsync(pendingDispatch, outboxMessageRepository, tenant.Id, now, cancellationToken);
            auditContext.RecordChange("Tenant", tenant.Id, before, CompanyLifecycleSnapshot.Of(tenant));

            return BusinessResult.Success(new Response(tenant.State, tenant.WindDownFrom));
        }
    }
}
