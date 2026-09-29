using Cleansia.Core.AppServices.Features.CashHeld.DTOs;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.CashHeld.Mappers;

public static class CashHeldMapper
{
    public static CleanerCashHeldDto MapToCleanerDto(this CashHeldBalance balance) =>
        new(
            EmployeeId: balance.EmployeeId,
            EmployeeName: balance.EmployeeName,
            CurrencyId: balance.CurrencyId,
            CurrencyCode: balance.CurrencyCode,
            Amount: balance.Amount);

    public static CashHeldDto MapToDto(this CashHeldBalance balance) =>
        new(
            CurrencyId: balance.CurrencyId,
            CurrencyCode: balance.CurrencyCode,
            Amount: balance.Amount);
}
