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

        // Gross-inclusive formula: vat = gross * rate / (1 + rate).
        // Example: 500 Kč at 21% → vat = 500 * 0.21 / 1.21 = 86.78 Kč, net = 413.22 Kč.
        //
        // RATE IS A FRACTION, NOT A PERCENT. StandardVatRate is numeric(5,4) — maximum 9.9999 — so
        // the column physically cannot hold 21, and Postgres rejects the insert if you try. Every
        // seeded value is a fraction (CZE 0.21, SVK 0.20, POL 0.23). This method previously divided
        // by (100 + rate), which is the percent form, and returned 4.19 Kč on a 2 000 Kč order
        // instead of 347.11 — a 98.8% under-declaration on every sale. It went unnoticed because
        // the seeded company is flagged not a VAT payer, so the guard above returns first.
        // CountryInvoiceContext.VatWithinGross has always used the fraction form; this now agrees.
        var vatAmount = Math.Round(
            totalPrice * rate / (1m + rate),
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
