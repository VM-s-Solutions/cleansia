using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.InvoiceTemplates;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.EmployeePayroll;

public class EmployeeInvoice : Auditable
{
    [Required]
    public string EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }

    [Required]
    public string PayPeriodId { get; private set; }
    public PayPeriod? PayPeriod { get; private set; }

    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; private set; }

    [Required]
    public int TotalOrders { get; private set; }

    [Required]
    public decimal SubTotal { get; private set; }

    public decimal BonusAmount { get; private set; } = 0;

    public decimal DeductionAmount { get; private set; } = 0;

    [Required]
    public decimal TotalAmount { get; private set; }

    [Required]
    public string CurrencyId { get; private set; }
    public Currency? Currency { get; private set; }

    [Required]
    public EmployeeInvoiceStatus Status { get; private set; } = EmployeeInvoiceStatus.Pending;

    [MaxLength(500)]
    public string? PdfBlobUrl { get; private set; }

    public string? TemplateId { get; private set; }
    public InvoiceTemplate? Template { get; private set; }

    public string? CountryId { get; private set; }
    public Country? Country { get; private set; }

    public string? LanguageId { get; private set; }
    public Language? Language { get; private set; }

    [Required]
    public DateTime GeneratedAt { get; private set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; private set; }

    public string? ApprovedBy { get; private set; }

    public DateTime? PaidAt { get; private set; }

    [MaxLength(1000)]
    public string? AdminNotes { get; private set; }

    [Required]
    [MaxLength(10)]
    public string VariableSymbol { get; private set; }

    [MaxLength(10)]
    public string? SpecificSymbol { get; private set; }

    [MaxLength(500)]
    public string? BankTransferNote { get; private set; }

    private ICollection<OrderEmployeePay> _orderPays = [];
    public IReadOnlyCollection<OrderEmployeePay> OrderPays => _orderPays.ToList().AsReadOnly();

    public static EmployeeInvoice Create(
        string employeeId,
        string payPeriodId,
        int totalOrders,
        decimal subTotal,
        string currencyId,
        decimal bonusAmount = 0,
        decimal deductionAmount = 0)
    {
        var totalAmount = subTotal + bonusAmount - deductionAmount;

        if (totalAmount < 0)
        {
            totalAmount = 0;
        }

        return new EmployeeInvoice
        {
            EmployeeId = employeeId,
            PayPeriodId = payPeriodId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString("N")[..5].ToUpper()}",
            TotalOrders = totalOrders,
            SubTotal = subTotal,
            BonusAmount = bonusAmount,
            DeductionAmount = deductionAmount,
            TotalAmount = totalAmount,
            CurrencyId = currencyId,
            Status = EmployeeInvoiceStatus.Pending,
            GeneratedAt = DateTime.UtcNow,
            VariableSymbol = GenerateVariableSymbol(employeeId, payPeriodId)
        };
    }

    public EmployeeInvoice AddOrderPays(IEnumerable<OrderEmployeePay> orderPays)
    {
        foreach (var pay in orderPays)
        {
            _orderPays.Add(pay);
        }

        TotalOrders = _orderPays.Count;
        SubTotal = _orderPays.Sum(p => p.TotalPay);
        TotalAmount = SubTotal + BonusAmount - DeductionAmount;

        if (TotalAmount < 0)
        {
            TotalAmount = 0;
        }

        return this;
    }

    public EmployeeInvoice SetPdfBlobUrl(string pdfBlobUrl)
    {
        PdfBlobUrl = pdfBlobUrl;
        return this;
    }

    public EmployeeInvoice SetSpecificSymbol(string? specificSymbol)
    {
        SpecificSymbol = specificSymbol;
        return this;
    }

    public EmployeeInvoice AssignTemplate(string templateId, string countryId, string languageId)
    {
        TemplateId = templateId;
        CountryId = countryId;
        LanguageId = languageId;
        return this;
    }

    public EmployeeInvoice Approve(string approvedBy, string? adminNotes = null)
    {
        if (Status != EmployeeInvoiceStatus.Pending && Status != EmployeeInvoiceStatus.Disputed)
        {
            throw new InvalidOperationException($"Cannot approve invoice in status {Status}");
        }

        Status = EmployeeInvoiceStatus.Approved;
        ApprovedAt = DateTime.UtcNow;
        ApprovedBy = approvedBy;
        AdminNotes = adminNotes;

        return this;
    }

    public EmployeeInvoice MarkAsPaid(string? bankTransferNote = null, string? adminNotes = null)
    {
        if (Status != EmployeeInvoiceStatus.Approved)
        {
            throw new InvalidOperationException($"Cannot mark as paid. Invoice must be approved first. Current status: {Status}");
        }

        Status = EmployeeInvoiceStatus.Paid;
        PaidAt = DateTime.UtcNow;
        BankTransferNote = bankTransferNote;

        if (adminNotes != null)
        {
            AdminNotes = adminNotes;
        }

        return this;
    }

    public EmployeeInvoice Dispute(string adminNotes)
    {
        if (Status == EmployeeInvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Cannot dispute a paid invoice");
        }

        Status = EmployeeInvoiceStatus.Disputed;
        AdminNotes = adminNotes;

        return this;
    }

    public EmployeeInvoice Reject(string adminNotes)
    {
        if (Status == EmployeeInvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Cannot reject a paid invoice");
        }

        Status = EmployeeInvoiceStatus.Rejected;
        AdminNotes = adminNotes;

        return this;
    }

    public EmployeeInvoice UpdateAmounts(decimal bonusAmount, decimal deductionAmount, string? adminNotes = null)
    {
        if (Status == EmployeeInvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Cannot update amounts for a paid invoice");
        }

        BonusAmount = bonusAmount;
        DeductionAmount = deductionAmount;
        TotalAmount = SubTotal + bonusAmount - deductionAmount;

        if (TotalAmount < 0)
        {
            TotalAmount = 0;
        }

        if (adminNotes != null)
        {
            AdminNotes = adminNotes;
        }

        return this;
    }

    public string GenerateInvoiceNumber(string prefix = "EMP")
    {
        var employeeShort = EmployeeId.Substring(0, Math.Min(6, EmployeeId.Length)).ToUpper();
        var periodShort = PayPeriodId.Substring(0, Math.Min(6, PayPeriodId.Length)).ToUpper();
        return $"{prefix}-{periodShort}-{employeeShort}";
    }

    public static string GenerateVariableSymbol(string employeeId, string payPeriodId)
    {
        var empHash = Math.Abs(employeeId.GetHashCode()) % 10000;
        var periodHash = Math.Abs(payPeriodId.GetHashCode()) % 1000000;
        return $"{empHash:D4}{periodHash:D6}";
    }

    public decimal CalculateAveragePay()
    {
        return TotalOrders > 0 ? SubTotal / TotalOrders : 0;
    }
}
