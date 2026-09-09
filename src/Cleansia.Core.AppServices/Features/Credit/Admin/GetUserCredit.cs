using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Credit.Admin;

/// <summary>
/// One customer's credit balance and ledger, for the admin about to change it.
///
/// <para>Reads only, and it deliberately does NOT open an account the way the loyalty mirror does:
/// looking at a customer is not a reason to give them one, and "never had credit" is a real answer an
/// admin wants to see rather than a zero-balance account that implies otherwise.</para>
///
/// <para>The ledger is the point. A balance alone cannot answer the question an admin actually has
/// before issuing — <i>has someone already compensated this person for this?</i> — and that question
/// is the whole of the human-in-the-loop ruling.</para>
/// </summary>
public class GetUserCredit
{
    public record Query(string UserId) : IQuery<Response>;

    /// <param name="HasAccount">
    /// False for a customer who has never been credited. Distinguished from a zero balance because
    /// the two mean different things to the person reading the screen.
    /// </param>
    public record Response(
        string UserId,
        bool HasAccount,
        decimal Balance,
        string CurrencyCode,
        IEnumerable<LedgerEntry> Ledger);

    /// <param name="Amount">Signed: positive was given, negative was spent.</param>
    /// <param name="Note">
    /// The admin's own words at the time. Null on the automatic rows — a spend at checkout and a
    /// return on cancellation both explain themselves through <paramref name="Reason"/> and
    /// <paramref name="OrderId"/>.
    /// </param>
    public record LedgerEntry(
        string Id,
        decimal Amount,
        CreditTransactionReason Reason,
        string? OrderId,
        string? DisputeId,
        string? Note,
        DateTimeOffset CreatedOn);

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.UserNotFound);
        }
    }

    public class Handler(
        ICreditAccountRepository creditAccountRepository,
        ICurrencyRepository currencyRepository) : IQueryHandler<Query, Response>
    {
        /// <summary>
        /// A ceiling on how much history one screen returns. An account that has been spent and
        /// returned on every booking for two years has an unbounded ledger, and this endpoint exists
        /// to answer "what happened lately", not to be an export.
        /// </summary>
        private const int MaxLedgerEntries = 100;

        public async Task<BusinessResult<Response>> Handle(
            Query request, CancellationToken cancellationToken)
        {
            // Largest balance first -- see GetMyCredit for why this is not pinned to the platform
            // default. Same answer as before for any customer holding one account.
            var account = (await creditAccountRepository.GetAllForUserAsync(
                request.UserId, cancellationToken)).FirstOrDefault();

            if (account == null)
            {
                var platformCurrency = await currencyRepository.GetDefaultAsync(cancellationToken);
                return BusinessResult.Success(new Response(
                    UserId: request.UserId,
                    HasAccount: false,
                    Balance: 0m,
                    CurrencyCode: platformCurrency?.Code ?? string.Empty,
                    Ledger: []));
            }

            var currency = await currencyRepository.GetByIdAsync(account.CurrencyId, cancellationToken);

            var ledger = account.Transactions
                .OrderByDescending(t => t.CreatedOn)
                .ThenByDescending(t => t.Id)
                .Take(MaxLedgerEntries)
                .Select(t => new LedgerEntry(
                    Id: t.Id,
                    Amount: t.Amount,
                    Reason: t.Reason,
                    OrderId: t.OrderId,
                    DisputeId: t.DisputeId,
                    Note: t.Note,
                    CreatedOn: t.CreatedOn))
                .ToList();

            return BusinessResult.Success(new Response(
                UserId: request.UserId,
                HasAccount: true,
                Balance: account.Balance,
                CurrencyCode: currency?.Code ?? string.Empty,
                Ledger: ledger));
        }
    }
}
