namespace Cleansia.Infra.Services.Pdf.Models;

public record InvoiceField(string Label, string? Value);

public record InvoiceLabels
{
    public string DocumentTitle { get; init; } = "Invoice";
    public string InvoiceNumber { get; init; } = "Invoice #";
    public string IssueDate { get; init; } = "Issue date";
    public string DueDate { get; init; } = "Due date";

    public string Supplier { get; init; } = "Supplier";
    public string Customer { get; init; } = "Customer";
    public string ContactDetails { get; init; } = "Contact details";

    public string Name { get; init; } = "Name";
    public string Address { get; init; } = "Address";
    public string Country { get; init; } = "Country";
    public string RegistrationNumber { get; init; } = "Reg. No.";
    public string VatNumber { get; init; } = "VAT No.";
    public string VatStatus { get; init; } = "VAT";
    public string NotVatRegistered { get; init; } = "Not registered for VAT";
    public string Email { get; init; } = "E-mail";
    public string Phone { get; init; } = "Telephone";

    public string PaymentDetails { get; init; } = "Payment details";
    public string BankName { get; init; } = "Bank";
    public string BankAccount { get; init; } = "Account number";
    public string Iban { get; init; } = "IBAN";
    public string Swift { get; init; } = "SWIFT";
    public string VariableSymbol { get; init; } = "Variable symbol";
    public string ConstantSymbol { get; init; } = "Constant symbol";
    public string PaymentMethod { get; init; } = "Payment method";
    public string BankTransfer { get; init; } = "Bank transfer";
    public string AmountDue { get; init; } = "Amount due";

    public string PayPeriod { get; init; } = "Pay period";
    public string PayPeriodFrom { get; init; } = "From";
    public string PayPeriodTo { get; init; } = "To";

    public string LineItems { get; init; } = "Items";
    public string Description { get; init; } = "Description";
    public string Quantity { get; init; } = "Qty";
    public string Unit { get; init; } = "Unit";
    public string UnitOfMeasure { get; init; } = "pc";
    public string UnitPrice { get; init; } = "Unit price";
    public string LineTotal { get; init; } = "Total";
    public string LineDescription { get; init; } = "Cleaning services — order";

    public string SubTotal { get; init; } = "Subtotal";
    public string Bonus { get; init; } = "Bonus";
    public string Deduction { get; init; } = "Deduction";
    public string Vat { get; init; } = "VAT";

    public string LegalNotice { get; init; } = "Legal notice";
    public string GeneratedOn { get; init; } = "Generated";

    /// <summary>
    /// The statutory late-payment interest clause every locale states in its own words. Distinct from
    /// <c>InvoicePdfData.LegalDisclaimer</c>, which is per-country free text an admin edits.
    /// </summary>
    public string LatePaymentInterestNotice { get; init; } =
        "Please note that if payment is not received by the due date stated on this invoice, " +
        "we may charge statutory interest on the overdue amount.";

    public static InvoiceLabels Czech { get; } = new()
    {
        DocumentTitle = "Faktura",
        InvoiceNumber = "Faktura č.",
        IssueDate = "Datum vystavení",
        DueDate = "Datum splatnosti",

        Supplier = "Dodavatel",
        Customer = "Odběratel",
        ContactDetails = "Kontaktní údaje",

        Name = "Název",
        Address = "Adresa",
        Country = "Země",
        RegistrationNumber = "IČ",
        VatNumber = "DIČ",
        VatStatus = "DPH",
        NotVatRegistered = "Nejsme plátci DPH",
        Email = "E-mail",
        Phone = "Telefon",

        PaymentDetails = "Platební údaje",
        BankName = "Banka",
        BankAccount = "Číslo účtu",
        Iban = "IBAN",
        Swift = "SWIFT",
        VariableSymbol = "Variabilní symbol",
        ConstantSymbol = "Konstantní symbol",
        PaymentMethod = "Způsob platby",
        BankTransfer = "Bankovní převod",
        AmountDue = "Celkem k úhradě",

        PayPeriod = "Zúčtovací období",
        PayPeriodFrom = "Od",
        PayPeriodTo = "Do",

        LineItems = "Položky",
        Description = "Popis",
        Quantity = "Množství",
        Unit = "MJ",
        UnitOfMeasure = "ks",
        UnitPrice = "Cena za MJ",
        LineTotal = "Celkem",
        LineDescription = "Úklidové služby — objednávka",

        SubTotal = "Mezisoučet",
        Bonus = "Bonus",
        Deduction = "Srážka",
        Vat = "DPH",

        LegalNotice = "Právní upozornění",
        GeneratedOn = "Vystaveno",

        LatePaymentInterestNotice =
            "Dovolujeme si Vás upozornit, že v případě nedodržení data splatnosti uvedeného na faktuře " +
            "Vám můžeme účtovat zákonný úrok z prodlení."
    };
}
