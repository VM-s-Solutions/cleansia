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
        List<OrderEmployeePay> orderPays, CountryInvoiceContext? countryContext, CompanyInfo companyInfo,
        string dateFormat = "dd.MM.yyyy")
    {
        return new InvoicePdfData
        {
            InvoiceNumber = invoice.InvoiceNumber,
            VariableSymbol = invoice.VariableSymbol,
            PaymentReference = invoice.PaymentReference ?? invoice.VariableSymbol,
            GeneratedAt = invoice.GeneratedAt,
            EmployeeName = $"{employee.User?.FirstName} {employee.User?.LastName}",
            EmployeeAddress = employee.Address != null
                ? $"{employee.Address.Street}, {employee.Address.City}, {employee.Address.ZipCode}"
                : "N/A",
            EmployeeEmail = employee.User?.Email ?? "N/A",
            PayPeriodStart = invoice.PayPeriod!.StartDate.ToString(dateFormat),
            PayPeriodEnd = invoice.PayPeriod.EndDate.ToString(dateFormat),
            SubTotal = invoice.SubTotal,
            BonusAmount = invoice.BonusAmount,
            DeductionAmount = invoice.DeductionAmount,
            VatAmount = 0,
            TotalAmount = invoice.TotalAmount,
            CurrencyCode = currency?.Code ?? "EUR",
            CurrencySymbol = currency?.Symbol ?? "€",
            Orders = orderPays.Select(op => new OrderLineItem
            {
                OrderNumber = op.Order?.DisplayOrderNumber ?? "N/A",
                CompletedAt = op.Order?.CleaningDateTime ?? DateTime.UtcNow,
                BasePay = op.BasePay,
                ExtrasPay = op.ExtrasPay,
                ExpensesPay = op.ExpensesPay,
                TotalPay = op.TotalPay
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
}