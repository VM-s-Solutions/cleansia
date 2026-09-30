namespace Cleansia.Core.AppServices.Features.CashHeld.DTOs;

/// <summary>
/// The company's cash the calling cleaner holds in one currency, and the company's float cap: above it,
/// cash jobs are hidden from the cleaner's board, which <see cref="CashJobsHidden"/> states. A null cap is
/// no cap.
/// </summary>
public record CashHeldDto(
    string CurrencyId,
    string CurrencyCode,
    decimal Amount,
    decimal? FloatCap,
    bool CashJobsHidden);
