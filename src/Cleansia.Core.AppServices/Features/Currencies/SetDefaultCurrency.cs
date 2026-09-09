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

            // THE DEFAULT CURRENCY IS THE PRICING CURRENCY, so it may only ever be one the catalogue is
            // actually priced in. Until per-currency price tables exist (Wave B) the catalogue carries a
            // single unlabelled set of numbers, and the pricing calculator no longer scales them by an
            // exchange rate — so promoting a second currency here would charge the CZK figures under its
            // code. On the seeded basket that is roughly a 25x overcharge, on the card and on the fiscal
            // receipt, from one star icon in Admin -> Currencies.
            //
            // `IsActive` is the gate, and this is its FIRST reader anywhere in the platform: no
            // repository predicate, no query filter and no endpoint consulted it before. Wave B replaces
            // the condition with "has price rows in this currency" and activates EUR in the same commit
            // that gives it those rows.
            if (!currency.IsActive)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CurrencyId), BusinessErrorMessage.InvalidCurrency));
            }

            var previousDefault = await currencyRepository.GetDefaultAsync(cancellationToken);

            // ORDERED, AND ATOMIC. `IX_Currencies_IsDefault_Unique` is a partial unique index, which
            // Postgres cannot defer -- it is checked at the end of every statement, so the moment both
            // rows are true is a violation rather than an intermediate state.
            //
            // Leaving both writes to the pipeline's single commit does not work, and not merely
            // sometimes: EF emits the two UPDATEs in the order the entities entered the CHANGE TRACKER,
            // not in the order they were mutated. This handler loads the promote target first (the
            // GetByIdAsync above) and the current default second, so the promote would ALWAYS be
            // emitted first and every promote would be a 23505. Reordering the two loads would fix
            // today's code and leave the next reader one innocent-looking edit away from breaking it,
            // which is why the ordering is made explicit here instead.
            //
            // So the clear is flushed first, and the promote second. The transaction is what keeps the
            // window between them from being observable: `GetDefaultAsync` throws when no default
            // exists, and it has roughly fifteen production callers including the recurring materializer
            // and the pay-period background service -- a durable zero-default gap would take order
            // creation down, not just this screen. Both flushes are ordinary `CommitAsync` calls, so
            // both rows still get their audit stamp.
            //
            // WHAT THIS COSTS. The pipeline commit afterwards is NOT a no-op: AuditLogBehavior is
            // registered inner to the UnitOfWork behavior so its AdminActionAudit row rides that
            // commit, and this command is audited. Committing here means the promote is durable before
            // the audit row exists, so a failure in between leaves a currency change with no audit
            // trail. The eight other handlers that flush early -- CreateAdminUser, Register,
            // RegisterEmployee, CreatePromoCode, TakeOrder, GenerateInvoice among them -- all share
            // that property, so it is a pattern-wide question rather than one this handler invented.
            // It is accepted here because the alternative is a 500 on a promote whose statement order
            // nobody controls, which is the worse of the two.
            await using var transaction = await currencyRepository.BeginTransactionAsync(cancellationToken);

            previousDefault.SetAsDefault(false);
            await currencyRepository.CommitAsync(cancellationToken);

            currency.SetAsDefault(true);
            await currencyRepository.CommitAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return BusinessResult.Success(new Response(currency.Id));
        }
    }
}
