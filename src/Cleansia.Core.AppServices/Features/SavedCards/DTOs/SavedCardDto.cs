namespace Cleansia.Core.AppServices.Features.SavedCards.DTOs;

public record SavedCardDto(
    string Id,
    string Brand,
    string Last4,
    int ExpMonth,
    int ExpYear,
    string CurrencyCode);
