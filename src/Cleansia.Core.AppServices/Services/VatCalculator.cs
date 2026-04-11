using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;

namespace Cleansia.Core.AppServices.Services;

public sealed class VatCalculator : IVatCalculator
{
    public VatBreakdown Calculate(
        decimal totalPrice,
        CompanyInfo companyInfo,
        CountryConfiguration? countryConfig)
    {
        if (!companyInfo.IsVatPayer || countryConfig == null)
        {
            return VatBreakdown.NotApplicable(totalPrice);
        }

        var rate = countryConfig.StandardVatRate;

        // Gross-inclusive formula: vat = gross * rate / (100 + rate)
        // Example: 500 Kč at 21% → vat = 500 * 21 / 121 = 86.78 Kč, net = 413.22 Kč
        var vatAmount = Math.Round(
            totalPrice * rate / (100 + rate),
            2,
            MidpointRounding.AwayFromZero);
        var netAmount = totalPrice - vatAmount;

        return new VatBreakdown(
            NetAmount: netAmount,
            VatAmount: vatAmount,
            AppliedRate: rate,
            IsApplicable: true);
    }
}
