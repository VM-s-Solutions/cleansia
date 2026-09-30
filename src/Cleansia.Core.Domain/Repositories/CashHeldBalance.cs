namespace Cleansia.Core.Domain.Repositories;

/// <summary>The cash one cleaner holds in one currency: the sum of their ledger entries in it.</summary>
public sealed record CashHeldBalance(
    string EmployeeId,
    string EmployeeName,
    string CurrencyId,
    string CurrencyCode,
    decimal Amount);
