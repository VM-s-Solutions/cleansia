using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Calculates the VAT breakdown for an order total, given the company's VAT payer status
/// and the country's configured VAT rates. Pure function — no I/O, safe to call synchronously.
/// </summary>
public interface IVatCalculator
{
    VatBreakdown Calculate(
        decimal totalPrice,
        CompanyInfo companyInfo,
        CountryConfiguration? countryConfig);
}

/// <summary>
/// Result of a VAT calculation. When the company is not a VAT payer, <see cref="IsApplicable"/> is false
/// and <see cref="AppliedRate"/> is null — use <see cref="NotApplicable"/> to build this case.
/// </summary>
public record VatBreakdown(
    decimal NetAmount,
    decimal VatAmount,
    decimal? AppliedRate,
    bool IsApplicable)
{
    public static VatBreakdown NotApplicable(decimal totalPrice) =>
        new(NetAmount: totalPrice, VatAmount: 0m, AppliedRate: null, IsApplicable: false);
}
