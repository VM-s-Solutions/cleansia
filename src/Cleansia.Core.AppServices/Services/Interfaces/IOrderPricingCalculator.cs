namespace Cleansia.Core.AppServices.Services.Interfaces;

public record OrderPricingResult(
    decimal TotalPrice,
    string CurrencyId,
    string CurrencyCode,
    decimal ServicesSubtotal,
    decimal PackagesSubtotal,
    decimal ExchangeRate);

public interface IOrderPricingCalculator
{
    Task<OrderPricingResult> CalculateAsync(
        IEnumerable<string> selectedServiceIds,
        IEnumerable<string> selectedPackageIds,
        int rooms,
        int bathrooms,
        string? currencyId,
        CancellationToken cancellationToken);
}
