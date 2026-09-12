using Microsoft.EntityFrameworkCore;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Currencies;

/// <summary>
/// Promotes a currency to the platform default, clearing the previous one first, in one transaction.
/// Preserves the exactly-one-default invariant that the delete protection
/// (CannotDeleteDefaultCurrency) and the overview sort rely on -- an invariant the DATABASE now holds
/// too, via the partial unique index `IX_Currencies_IsDefault_Unique`. Idempotent on the current
/// default.
///
/// <para>It no longer mirrors SetDefaultSavedAddress, and that sibling is NOT safe. It does the same
/// clear-then-set with no flush and no transaction, under a filtered unique index of its own
/// (IX_SavedAddresses_UserId_Default_Unique) that is non-deferrable for exactly the reason described
/// below. Being per-user does not help: both rows belong to the same user, and both are active. The
/// mechanism applies verbatim. That is a live defect on a customer path and it is reported rather than
/// fixed here, because it is outside this change.</para>
/// </summary>
public class SetDefaultCurrency
{
    public record Command(string CurrencyId) : ICommand<Response>;

    public record Response(string CurrencyId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.CurrencyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(currencyRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.CurrencyNotFound);
        }
    }

    public class Handler(ICurrencyRepository currencyRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var currency = await currencyRepository.GetByIdAsync(command.CurrencyId, cancellationToken);
            if (currency is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CurrencyId), BusinessErrorMessage.CurrencyNotFound));
            }

            if (currency.IsDefault)
            {
                return BusinessResult.Success(new Response(currency.Id));
            }

            // THE DEFAULT IS WHAT EVERY QUOTE FALLS BACK TO, so it may only ever be a currency the
            // platform sells in AND has something to sell in. Two halves, checked in that order so the
            // admin is told which one is missing, and the same two halves the booking path checks a
            // caller-named currency against -- ICurrencyRepository.IsOfferableAsync is the one
            // definition, so the star and the quote cannot disagree about what "offerable" means.
            //
            // ACTIVE is the market switch. Currency.Create makes a row switched off and ActivateCurrency
            // is the only writer of true; the catalogue price rule reads it to decide which currencies
            // every entry must be priced in.
            if (!currency.IsActive)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CurrencyId), BusinessErrorMessage.InvalidCurrency));
            }

            // PRICED is what stops an active currency with an empty catalogue becoming the default.
            // Every customer overview fails closed on the price rows, so promoting one would withhold
            // every entry from every customer until someone promoted the old default back. IsActive
            // alone was vacuous here for any currency an admin could create, which is why this exists.
            if (!await currencyRepository.IsOfferableAsync(currency.Id, cancellationToken))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CurrencyId), BusinessErrorMessage.CurrencyNotPriced));
            }

            // ORDERED, AND ATOMIC. `IX_Currencies_IsDefault_Unique` is a partial unique index, which
            // Postgres cannot defer -- it is checked at the end of every statement, so the moment both
            // rows are true is a violation rather than an intermediate state.
            //
            // Leaving both writes to the pipeline's single commit does not work, and not merely
            // sometimes: EF emits the two UPDATEs in the order the entities entered the CHANGE TRACKER,
            // not in the order they were mutated. This handler loads the promote target first (the
            // GetByIdAsync above), so the promote would ALWAYS be emitted first and every promote would
            // be a 23505. Reordering the loads would fix today's code and leave the next reader one
            // innocent-looking edit away from breaking it, which is why the ordering is explicit here.
            //
            // The clear takes EVERY default rather than the one a prior read named: reading first and
            // clearing that row leaves the promote racing a snapshot it no longer holds, and it cannot
            // recover a database that has none. Clearing zero rows is a valid outcome.
            //
            // The transaction is what keeps the window between the two flushes unobservable:
            // `GetDefaultAsync` throws when no default exists, and it has roughly fifteen production
            // callers including the recurring materializer and the pay-period background service -- a
            // durable zero-default gap would take order creation down, not just this screen. Both
            // flushes are ordinary `CommitAsync` calls, so both rows still get their audit stamp.
            //
            // WHAT THIS COSTS, stated because it is a real trade and not a free one. The pipeline
            // commit afterwards is NOT a no-op: AuditLogBehavior is registered inner to the UnitOfWork
            // behavior so its AdminActionAudit row rides that commit, and this command is audited.
            // Committing here means the promote is durable before the audit row exists. Eight other
            // admin commands flush early and share that property, so it is a pattern-wide question
            // rather than one this handler invented -- and it is accepted here because the alternative
            // is a promote that cannot succeed at all. The nine are enumerated once, with what is and
            // is not lost, in ADR-0012's 2026-09-10 amendment -> /decisions/adr-0012#later-amendment-2026-09-10-the-early-flush-carve-out
            await using var transaction = await currencyRepository.BeginTransactionAsync(cancellationToken);

            await currencyRepository.ClearDefaultAsync(cancellationToken);
            await currencyRepository.CommitAsync(cancellationToken);

            currency.SetAsDefault(true);

            // OWN THE LOSER'S 23505. Two admins promoting different currencies in the same second both
            // clear, and the second one's promote lands while the first's is already true. Without this
            // the index -- which exists precisely to make that impossible -- reports it as an
            // untranslated 500 instead of the error key the client already has.
            try
            {
                await currencyRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
                when (DbConstraintViolation.IsUniqueViolation(ex))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CurrencyId), BusinessErrorMessage.CurrencyDefaultChangedConcurrently));
            }

            await transaction.CommitAsync(cancellationToken);

            return BusinessResult.Success(new Response(currency.Id));
        }
    }
}
