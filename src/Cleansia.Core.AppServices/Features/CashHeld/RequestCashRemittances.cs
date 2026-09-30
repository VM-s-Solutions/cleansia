using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.CashHeld;

/// <summary>
/// Owner ruling 2026-09-28, decision 23: cash a cleaner holds that a pay-period close could not set off
/// against their pay is carried forward, and once it has been carried longer than the company's number of
/// days (<see cref="TenantSettingCatalog.CashRemittanceRequestDays"/>, 30 unless it sets another) the cleaner
/// is e-mailed a request to hand it over. A balance is the run of a cleaner's cash in one currency above
/// zero; it is carried from the first close after it began, and it is asked about once — a balance that
/// falls to zero and rises again is a new one. Runs company by company under each company's override and
/// commits inside the loop; a company frozen for archive is its administrators' to settle.
/// </summary>
public class RequestCashRemittances
{
    public record Command : ICommand<Response>;

    public class Validator : AbstractValidator<Command>;

    public record Response(int Requested);

    public class Handler(
        ITenantRepository tenantRepository,
        ITenantProvider tenantProvider,
        IAppConfigurationProvider configurationProvider,
        ICashLedgerRepository cashLedgerRepository,
        IPayPeriodRepository payPeriodRepository,
        IEmployeeRepository employeeRepository,
        ICurrencyRepository currencyRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var requested = 0;

            foreach (var tenantId in await tenantRepository.GetAllIdsAsync(cancellationToken))
            {
                tenantProvider.ClearTenantOverride();
                tenantProvider.SetTenantOverride(tenantId);

                var company = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
                if (company is null || company.IsFrozen)
                {
                    continue;
                }

                requested += await RequestForCompanyAsync(now, cancellationToken);
                await unitOfWork.CommitAsync(cancellationToken);
            }

            tenantProvider.ClearTenantOverride();
            return BusinessResult.Success(new Response(requested));
        }

        private async Task<int> RequestForCompanyAsync(DateTime now, CancellationToken cancellationToken)
        {
            var holding = (await cashLedgerRepository.GetBalancesAsync(null, cancellationToken))
                .Where(b => b.Amount > 0m)
                .ToList();
            if (holding.Count == 0)
            {
                return 0;
            }

            var days = await configurationProvider.GetAsync(TenantSettingCatalog.CashRemittanceRequestDays, cancellationToken);
            var closes = await payPeriodRepository.GetQueryable()
                .Where(p => p.ClosedAt != null)
                .Select(p => p.ClosedAt!.Value)
                .ToListAsync(cancellationToken);
            var entries = await cashLedgerRepository.GetForEmployeesAsync(
                holding.Select(b => b.EmployeeId).Distinct().ToList(), cancellationToken);

            var requested = 0;
            foreach (var balance in holding)
            {
                var balanceStart = StartOfBalance(
                    entries.Where(e => e.EmployeeId == balance.EmployeeId && e.CurrencyId == balance.CurrencyId));
                if (balanceStart is null || balanceStart.RemittanceRequestedAt is not null)
                {
                    continue;
                }

                var carriedSince = closes.Where(c => c > balanceStart.OccurredAt).Order().FirstOrDefault();
                if (carriedSince == default || now - carriedSince <= TimeSpan.FromDays(days))
                {
                    continue;
                }

                if (await RequestAsync(balance, carriedSince, cancellationToken))
                {
                    balanceStart.MarkRemittanceRequested(now);
                    requested++;
                }
            }

            return requested;
        }

        /// <summary>The entry that took the cleaner's cash in the currency above zero for the last time, or null.</summary>
        private static CashLedgerEntry? StartOfBalance(IEnumerable<CashLedgerEntry> chronological)
        {
            var held = 0m;
            CashLedgerEntry? start = null;
            foreach (var entry in chronological)
            {
                var before = held;
                held += entry.Amount;
                if (held <= 0m)
                {
                    start = null;
                }
                else if (before <= 0m)
                {
                    start = entry;
                }
            }

            return start;
        }

        private async Task<bool> RequestAsync(CashHeldBalance balance, DateTime carriedSince, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByIdAsync(balance.EmployeeId, cancellationToken);
            if (employee?.User is not { Email.Length: > 0 } user)
            {
                logger.LogWarning(
                    "Cleaner {EmployeeId} holds carried cash but has no e-mail address; no remittance request sent",
                    balance.EmployeeId);
                return false;
            }

            var currency = await currencyRepository.GetByIdAsync(balance.CurrencyId, cancellationToken);
            try
            {
                await emailService.SendCashRemittanceRequestEmailAsync(
                    user.Email,
                    $"{user.FirstName} {user.LastName}".Trim(),
                    balance.Amount,
                    currency?.Symbol ?? balance.CurrencyCode,
                    carriedSince,
                    user.PreferredLanguageCode ?? Constants.Language.English,
                    cancellationToken);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Remittance request to cleaner {EmployeeId} was not sent; the next run asks again", balance.EmployeeId);
                return false;
            }
        }
    }
}
