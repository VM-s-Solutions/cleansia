namespace Cleansia.Core.Domain.Credit;

/// <summary>
/// What checkout needs to know about a customer's credit: how much there is, in what currency, and
/// which account to debit. Projected untracked, without the ledger.
///
/// <para>A named record rather than a tuple because it crosses an assembly boundary and reaches two
/// call sites; a positional tuple would let a caller swap <c>Balance</c> and <c>CurrencyId</c> —
/// both of which are otherwise unremarkable — with no compiler complaint.</para>
/// </summary>
public record CreditSpendable(string AccountId, decimal Balance, string CurrencyId);
