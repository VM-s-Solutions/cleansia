namespace Cleansia.Core.AppServices.Features.CashHeld.DTOs;

/// <summary>The company's cash one cleaner holds in one currency.</summary>
public record CleanerCashHeldDto(
    string EmployeeId,
    string EmployeeName,
    string CurrencyId,
    string CurrencyCode,
    decimal Amount);
