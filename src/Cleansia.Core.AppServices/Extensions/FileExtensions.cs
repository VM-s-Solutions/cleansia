using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Core.AppServices.Extensions;

public static class FileExtensions
{
    /// <summary>
    /// Extracts the Base64 content by removing any data URI prefix if present.
    /// </summary>
    /// <param name="base64Content">The original Base64 string (possibly with a prefix).</param>
    /// <returns>The raw Base64 string.</returns>
    public static string ExtractBase64Data(this string base64Content)
    {
        if (string.IsNullOrWhiteSpace(base64Content))
        {
            return base64Content;
        }

        var parts = base64Content.Split(',');
        return parts.Length > 1 ? parts[1] : parts[0];
    }

    public static InvoicePdfData CreatePdfData(this EmployeeInvoice invoice, Employee employee, Currency? currency,
        IReadOnlyList<OrderEmployeePay> orderPays, CountryInvoiceContext? countryContext, CompanyInfo companyInfo,
        string dateFormat = "dd.MM.yyyy")
    {
        return new InvoicePdfData
        {
            InvoiceNumber = invoice.InvoiceNumber,
            VariableSymbol = invoice.VariableSymbol,
            PaymentReference = invoice.PaymentReference ?? invoice.VariableSymbol,
            GeneratedAt = invoice.GeneratedAt,
            DueDate = invoice.CalculateDueDate(Constants.PayoutInvoice.PaymentTermsDays),
            Supplier = employee.CreateSupplierData(),
            PayPeriodStart = invoice.PayPeriod!.StartDate.ToString(dateFormat),
            PayPeriodEnd = invoice.PayPeriod.EndDate.ToString(dateFormat),
            SubTotal = invoice.SubTotal,
            BonusAmount = invoice.BonusAmount,
            DeductionAmount = invoice.DeductionAmount,
            VatAmount = 0,
            TotalAmount = invoice.TotalAmount,
            CurrencyCode = currency?.Code ?? Constants.Currency.Czk,
            CurrencySymbol = currency?.Symbol ?? "Kč",
            LineItems = orderPays.Select(op => new InvoiceLineItem
            {
                OrderNumber = op.Order?.DisplayOrderNumber ?? "N/A",
                PerformedOn = op.Order?.CleaningDateTime ?? DateTime.UtcNow,
                Quantity = 1,
                UnitPrice = LineAmount(op),
                LineTotal = LineAmount(op)
            }).ToList(),
            LegalDisclaimer = countryContext?.LegalDisclaimerTemplate,
            Company = new CompanyInfoData
            {
                LegalName = companyInfo.LegalName,
                TradingName = companyInfo.TradingName,
                Tagline = companyInfo.Tagline,
                RegistrationNumber = companyInfo.RegistrationNumber,
                VatNumber = companyInfo.VatNumber,
                Street = companyInfo.Street,
                City = companyInfo.City,
                ZipCode = companyInfo.ZipCode,
                Address = companyInfo.GetFullAddress(),
                Phone = companyInfo.Phone,
                Email = companyInfo.Email,
                Website = companyInfo.Website,
                BankName = companyInfo.BankName,
                BankAccountNumber = companyInfo.BankAccountNumber,
                Iban = companyInfo.Iban,
                Swift = companyInfo.Swift,
                ContactInfo = companyInfo.GetFormattedContactInfo()
            }
        };
    }

    // One line = one completed job. TotalPay already folds the period bonus/deduction in, so backing
    // them out is what makes Σ lines equal the invoice's SubTotal — leaving them in would print them
    // once per line and again in the summary.
    private static decimal LineAmount(OrderEmployeePay pay) =>
        pay.TotalPay - pay.BonusPay + pay.DeductionPay;

    private static InvoiceSupplierData CreateSupplierData(this Employee employee)
    {
        // No dedicated "is a VAT payer" flag exists on Employee, unlike CompanyInfo.IsVatPayer. The
        // presence of a validated DIČ is the only signal the model carries today.
        var vatNumber = string.IsNullOrWhiteSpace(employee.VatNumber) ? null : employee.VatNumber;

        return new InvoiceSupplierData
        {
            Name = !string.IsNullOrWhiteSpace(employee.LegalEntityName)
                ? employee.LegalEntityName
                : $"{employee.User?.FirstName} {employee.User?.LastName}".Trim(),
            Street = employee.Address?.Street,
            ZipCode = employee.Address?.ZipCode,
            City = employee.Address?.City,
            Country = employee.Address?.Country?.Name,
            RegistrationNumber = employee.RegistrationNumber,
            VatNumber = vatNumber,
            IsVatPayer = vatNumber != null,
            Email = employee.User?.Email,
            Phone = employee.User?.PhoneNumber,
            Iban = employee.IBAN
        };
    }
}