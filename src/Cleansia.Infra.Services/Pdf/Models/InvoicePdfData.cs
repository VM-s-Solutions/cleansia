namespace Cleansia.Infra.Services.Pdf.Models;

public record InvoicePdfData
{
    public required string InvoiceNumber { get; init; }
    public string? VariableSymbol { get; init; }
    public string? ConstantSymbol { get; init; }
    public string? PaymentReference { get; init; }
    public required DateTime GeneratedAt { get; init; }
    public DateTime? DueDate { get; init; }

    public required InvoiceSupplierData Supplier { get; init; }

    public required string PayPeriodStart { get; init; }
    public required string PayPeriodEnd { get; init; }

    public required decimal SubTotal { get; init; }
    public required decimal BonusAmount { get; init; }
    public required decimal DeductionAmount { get; init; }
    public required decimal VatAmount { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string CurrencyCode { get; init; }
    public required string CurrencySymbol { get; init; }

    public required List<InvoiceLineItem> LineItems { get; init; }

    public string? LegalDisclaimer { get; init; }

    /// <summary>
    /// The CUSTOMER of a payout invoice — the platform pays the cleaner, so Cleansia is the
    /// Odběratel here. On the customer-facing receipt the same record is the issuer instead.
    /// </summary>
    public CompanyInfoData? Company { get; init; }
}

/// <summary>
/// The SUPPLIER of a payout invoice — the cleaner. Bank details are the cleaner's own; sourcing them
/// from <see cref="CompanyInfoData"/> would print an account that tells the cleaner to pay us.
/// </summary>
public record InvoiceSupplierData
{
    public required string Name { get; init; }
    public string? Street { get; init; }
    public string? ZipCode { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }

    public string? RegistrationNumber { get; init; }
    public string? VatNumber { get; init; }
    public required bool IsVatPayer { get; init; }

    public string? Email { get; init; }
    public string? Phone { get; init; }

    public string? BankName { get; init; }
    public string? BankAccountNumber { get; init; }
    public string? Iban { get; init; }
    public string? Swift { get; init; }
}

public record CompanyInfoData
{
    public required string LegalName { get; init; }
    public required string TradingName { get; init; }
    public string? Tagline { get; init; }

    public required string RegistrationNumber { get; init; }
    public string? VatNumber { get; init; }

    public required string Street { get; init; }
    public required string City { get; init; }
    public required string ZipCode { get; init; }
    public required string Address { get; init; }

    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public required string ContactInfo { get; init; }

    public string? BankName { get; init; }
    public string? BankAccountNumber { get; init; }
    public string? Iban { get; init; }
    public string? Swift { get; init; }
}
