using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.CompanyLifecycle;

/// <summary>
/// Seals the admin's own company (ADR-0064 D3). Admitted only once the door is closed, the people
/// have been told, every live fact of the books is settled and the chargeback horizon has passed —
/// a seal is only honest over settled books. The request itself is the freeze: from this commit on
/// the company's books refuse every write except the law's, and the bundle the consumer builds is a
/// pure function of frozen input. A frozen company whose build poisoned is asked again from here
/// with no second stamp — the folder is named by the first request.
/// </summary>
[AuditAction("company.archive", ResourceType = "Tenant")]
public class ArchiveCompany
{
    public record Command : ICommand<Response>;

    public record Response(CompanyLifecycleState State, DateTimeOffset? ArchiveRequestedOn);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            ITenantRepository tenantRepository,
            ITenantProvider tenantProvider,
            ICompanySettlementReader settlementReader,
            IAppConfigurationProvider configurationProvider,
            TimeProvider timeProvider)
        {
            Tenant? company = null;
            var companyRead = false;
            CompanySettlementFacts? facts = null;

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(async (_, ct) => await CompanyAsync(ct) is not null)
                .WithMessage(BusinessErrorMessage.TenantNotFound)
                .MustAsync(async (_, ct) => await CompanyAsync(ct) is { IsDeactivated: true })
                .WithMessage(BusinessErrorMessage.CompanyNotDeactivated)
                .MustAsync(async (_, ct) => await CompanyAsync(ct) is { IsWindDownRequested: true })
                .WithMessage(BusinessErrorMessage.CompanyWindDownNotRequested)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).OpenOrders == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasOpenOrders)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).OrdersAwaitingPay == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasOrdersAwaitingPay)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).OrdersAwaitingReceipt == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasOrdersAwaitingReceipt)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).ReceiptsAwaitingFiscalRegistration == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasReceiptsAwaitingFiscalRegistration)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).PendingRefunds == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasPendingRefunds)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).ActiveMemberships == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasActiveMemberships)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).CreditBalances == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasCreditBalances)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).OpenPayPeriods == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasOpenPayPeriod)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).UnpaidInvoices == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasUnpaidInvoices)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).UninvoicedPayRows == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasUninvoicedPay)
                .MustAsync(async (_, ct) => (await FactsAsync(ct)).OpenDisputes == 0)
                .WithMessage(BusinessErrorMessage.CompanyHasOpenDisputes)
                .MustAsync(async (_, ct) => !await WithinChargebackHorizonAsync(ct))
                .WithMessage(BusinessErrorMessage.CompanyWithinChargebackHorizon)
                .MustAsync(async (_, ct) => await CompanyAsync(ct) is { IsArchived: false })
                .WithMessage(BusinessErrorMessage.CompanyArchived)
                .OverridePropertyName(ErrorCode);

            async Task<Tenant?> CompanyAsync(CancellationToken ct)
            {
                if (!companyRead)
                {
                    var tenantId = tenantProvider.GetCurrentTenantId();
                    company = tenantId is null ? null : await tenantRepository.GetByIdAsync(tenantId, ct);
                    companyRead = true;
                }

                return company;
            }

            async Task<CompanySettlementFacts> FactsAsync(CancellationToken ct) =>
                facts ??= await settlementReader.ReadAsync(ct);

            async Task<bool> WithinChargebackHorizonAsync(CancellationToken ct)
            {
                var latestCardPaidClean = (await FactsAsync(ct)).LatestCardPaidCleaningDateTime;
                if (latestCardPaidClean is null)
                {
                    return false;
                }

                var horizonDays = await configurationProvider.GetAsync(TenantSettingCatalog.ChargebackHorizonDays, ct);
                return latestCardPaidClean.Value.AddDays(horizonDays) > timeProvider.GetUtcNow().UtcDateTime;
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
            if (!tenant.IsFrozen)
            {
                tenant.RequestArchive(userSessionProvider.GetUserId()!, now);
            }

            // The key is the instant the build was asked for, to the second: a double-clicked request
            // asks for one build and the row already there carries it; a later "build again" is a new
            // message. The folder it builds into is named by the freeze on the row, not by this key.
            var key = MessageKeys.CompanyArchive(tenant.Id, now);
            if (await outboxMessageRepository.GetByQueueAndKeyAsync(QueueNames.CompanyArchive, key, cancellationToken) is null)
            {
                pendingDispatch.Enqueue(
                    QueueNames.CompanyArchive,
                    new QueueEnvelope<CompanyArchiveMessage>(key, tenant.Id, new CompanyArchiveMessage(tenant.Id, tenant.ArchiveRequestedOn!.Value)),
                    key);
            }

            auditContext.RecordChange("Tenant", tenant.Id, before, CompanyLifecycleSnapshot.Of(tenant));

            return BusinessResult.Success(new Response(tenant.State, tenant.ArchiveRequestedOn));
        }
    }
}
