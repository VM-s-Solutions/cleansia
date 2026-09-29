namespace Cleansia.Core.AppServices.Features.CashHeld.DTOs;

/// <summary>The company's cash the calling cleaner holds in one currency.</summary>
public record CashHeldDto(
    string CurrencyId,
    string CurrencyCode,
    decimal Amount);
