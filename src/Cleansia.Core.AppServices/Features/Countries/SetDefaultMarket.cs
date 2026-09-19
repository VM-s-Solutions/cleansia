using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Countries;

/// <summary>
/// Flags one country's configuration as the default market — the market a customer surface
/// pre-selects before any choice is made (owner ruling 2026-09-13). Modelled on
/// <c>SetDefaultCurrency</c>: clear the current flag, set the new one, in one transaction, both
/// flushed so both rows carry their audit stamp; idempotent on the current default.
///
/// <para>The gate is the one <c>SetCountryServiced</c> applies when a country is switched on, plus
/// serviced itself, plus an operating company: a default market that <c>Market/GetOverview</c> would
/// not list is a pre-selection of nothing, and the operator resolver scopes every anonymous write that
/// names no market to the flagged country's operator (ADR-0061 D3) — a default nobody operates would
/// fail them all with <c>tenant.not_found</c>.</para>
/// </summary>
public class SetDefaultMarket
{
    public record Command(string CountryId) : ICommand<Response>;

    public record Response(string CountryId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            ICountryRepository countryRepository,
            ICountryConfigurationRepository countryConfigurationRepository,
            ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await countryRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryNotFound)
                .MustAsync(async (id, ct) => await countryRepository.IsServicedAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryNotServiced)
                .MustAsync(async (id, ct) => await MarketIsReadyAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryMarketNotReady)
                .MustAsync(async (id, ct) => await MarketHasOperatorAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryMarketNotReady);

            async Task<bool> MarketIsReadyAsync(string countryId, CancellationToken ct)
            {
                var configuration = await countryConfigurationRepository.GetByCountryIdAsync(countryId, ct);
                if (string.IsNullOrWhiteSpace(configuration?.DefaultCurrencyCode))
                {
                    return false;
                }

                var currency = await currencyRepository.GetByCodeAsync(configuration.DefaultCurrencyCode, ct);
                return currency is { IsActive: true };
            }

            async Task<bool> MarketHasOperatorAsync(string countryId, CancellationToken ct)
            {
                var configuration = await countryConfigurationRepository.GetByCountryIdAsync(countryId, ct);
                return configuration?.OperatorTenantId is not null;
            }
        }
    }

    public class Handler(ICountryConfigurationRepository countryConfigurationRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var configuration = await countryConfigurationRepository.GetByCountryIdAsync(command.CountryId, cancellationToken);
            if (configuration is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CountryId), BusinessErrorMessage.CountryMarketNotReady));
            }

            if (configuration.IsDefaultMarket)
            {
                return BusinessResult.Success(new Response(configuration.CountryId));
            }

            // ORDERED, AND ATOMIC. IX_CountryConfigurations_IsDefaultMarket_Unique is a partial unique
            // index Postgres cannot defer, so the clear is flushed before the promote is emitted; the
            // transaction keeps the flag-less instant between the two flushes unobservable. The full
            // reasoning, including why the pipeline's single commit cannot do this, is on
            // SetDefaultCurrency, which this mirrors.
            await using var transaction = await countryConfigurationRepository.BeginTransactionAsync(cancellationToken);

            await countryConfigurationRepository.ClearDefaultMarketAsync(cancellationToken);
            await countryConfigurationRepository.CommitAsync(cancellationToken);

            configuration.SetAsDefaultMarket(true);

            try
            {
                await countryConfigurationRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
                when (DbConstraintViolation.IsUniqueViolation(ex))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CountryId), BusinessErrorMessage.CountryDefaultMarketChangedConcurrently));
            }

            await transaction.CommitAsync(cancellationToken);

            return BusinessResult.Success(new Response(configuration.CountryId));
        }
    }
}
