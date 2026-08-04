using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The payout invoice's party direction, decided at the mapper. The cleaner is the SUPPLIER
/// (Dodavatel — they are the one being paid) and Cleansia is the CUSTOMER (Odběratel). The document
/// shipped before this pinned the two the other way round, which made it a different legal
/// instrument, and it printed Cleansia's bank account — telling the cleaner to pay us.
/// </summary>
public class PayoutInvoicePdfDataTests
{
    [Fact]
    public void Supplier_Is_The_Cleaner()
    {
        var data = Map();

        Assert.Equal("Jan Novák", data.Supplier.Name);
        Assert.Equal("Dlouhá 12", data.Supplier.Street);
        Assert.Equal("11000", data.Supplier.ZipCode);
        Assert.Equal("Praha", data.Supplier.City);
        Assert.Equal("Czechia", data.Supplier.Country);
    }

    [Fact]
    public void Supplier_Carries_The_Cleaners_Registration_Number()
    {
        var data = Map();

        Assert.Equal("12345678", data.Supplier.RegistrationNumber);
    }

    [Fact]
    public void Supplier_Contact_Details_Are_The_Cleaners_Email_And_Phone()
    {
        var data = Map();

        Assert.Equal("jan.novak@cleansia.test", data.Supplier.Email);
        Assert.Equal("+420777123456", data.Supplier.Phone);
    }

    [Fact]
    public void Supplier_Name_Uses_The_Legal_Entity_Name_When_The_Cleaner_Trades_As_One()
    {
        var employee = Cleaner();
        employee.UpdateBusinessIdentity(EmployeeEntityType.LegalEntity, "12345678", null, "Novák Cleaning s.r.o.");

        var data = Map(employee);

        Assert.Equal("Novák Cleaning s.r.o.", data.Supplier.Name);
    }

    [Fact]
    public void Customer_Is_Cleansia_With_Its_Registration_And_Vat_Numbers()
    {
        var data = Map();

        Assert.NotNull(data.Company);
        Assert.Equal("Cleansia s.r.o.", data.Company!.LegalName);
        Assert.Equal("87654321", data.Company.RegistrationNumber);
        Assert.Equal("CZ87654321", data.Company.VatNumber);
    }

    [Fact]
    public void Payment_Block_Carries_The_Cleaners_Bank_Account_Not_The_Companys()
    {
        var data = Map(payoutDetails: PayoutDetails());

        Assert.Equal("CZ3155000000005885638003", data.Supplier.Iban);
        Assert.NotEqual(data.Company!.Iban, data.Supplier.Iban);
    }

    // The three fields the layout has always rendered and the mapper never filled. The layout tests
    // supply them by hand, so a real invoice printed "—" for all three while every test stayed green.
    [Fact]
    public void Payment_Block_Carries_The_Local_Account_Number_Swift_And_Bank_Name_Too()
    {
        var data = Map(payoutDetails: PayoutDetails());

        Assert.Equal("5885638003/5500", data.Supplier.BankAccountNumber);
        Assert.Equal("RZBCCZPP", data.Supplier.Swift);
        Assert.Equal("Raiffeisenbank", data.Supplier.BankName);
    }

    // ADR-0034 D5.1 stores the parts zero-padded to canonicalize them; the local form is written
    // without that padding, and a prefix that is all zeros is not written at all.
    [Fact]
    public void Local_Account_Number_Drops_The_Canonical_Padding_And_Prints_A_Real_Prefix()
    {
        var padded = Map(payoutDetails: PayoutDetails(prefix: "000000", number: "0000123456"));
        var prefixed = Map(payoutDetails: PayoutDetails(prefix: "000019", number: "2000145399"));

        Assert.Equal("123456/5500", padded.Supplier.BankAccountNumber);
        Assert.Equal("19-2000145399/5500", prefixed.Supplier.BankAccountNumber);
    }

    // ADR-0034 D7: payout details captured before EmployeePayoutDetails existed were never backfilled,
    // so the legacy column is still the only destination some cleaners have.
    [Fact]
    public void A_Cleaner_With_No_Payout_Record_Still_Prints_The_Legacy_Iban_And_No_Local_Number()
    {
        var data = Map();

        Assert.Equal("CZ3155000000005885638003", data.Supplier.Iban);
        Assert.Null(data.Supplier.BankAccountNumber);
        Assert.Null(data.Supplier.Swift);
        Assert.Null(data.Supplier.BankName);
    }

    // ── the two symbols: one per invoice, one per country ─────────────

    [Fact]
    public void Constant_Symbol_Comes_From_The_Country_Invoice_Configuration()
    {
        var data = Map(countryContext: CzechContext(constantSymbol: "0308"));

        Assert.Equal("0308", data.ConstantSymbol);
    }

    [Fact]
    public void Constant_Symbol_Is_Absent_When_The_Country_Configures_None()
    {
        Assert.Null(Map(countryContext: CzechContext()).ConstantSymbol);
        Assert.Null(Map().ConstantSymbol);
    }

    [Fact]
    public void Variable_Symbol_Is_Not_Derived_From_The_Invoice_Number()
    {
        var invoice = Invoice();
        invoice.SetVariableSymbol(EmployeeInvoice.GenerateVariableSymbol("emp-1", "period-1"));

        var data = Map(invoice: invoice);

        Assert.NotEqual(data.InvoiceNumber, data.VariableSymbol);
    }

    // The specimen's variabilní symbol equals its invoice number because the owner's invoice numbers
    // are numeric. This platform's are not (INV-yyyyMM-XXXXX), and a VS is numeric and at most ten
    // digits — so the generated numeric symbol is what reaches the document, not the invoice number.
    [Fact]
    public void Variable_Symbol_Is_Carried_Through_And_Stays_A_Valid_Numeric_Symbol()
    {
        var invoice = Invoice();
        invoice.SetVariableSymbol(EmployeeInvoice.GenerateVariableSymbol("emp-1", "period-1"));

        var data = Map(invoice: invoice);

        Assert.Equal(invoice.VariableSymbol, data.VariableSymbol);
        Assert.Matches("^[0-9]{1,10}$", data.VariableSymbol!);
    }

    [Fact]
    public void Issue_Date_And_Due_Date_Are_Both_Present()
    {
        var invoice = Invoice(generatedAt: new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc));

        var data = Map(invoice: invoice);

        Assert.Equal(new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc), data.GeneratedAt);
        Assert.Equal(invoice.CalculateDueDate(Constants.PayoutInvoice.PaymentTermsDays), data.DueDate);
    }

    [Fact]
    public void Line_Items_Carry_A_Quantity_And_A_Unit_Price_Per_Completed_Job()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 300m, extrasPay: 50m, expensesPay: 25m),
            PayrollMockFactory.OrderPay(basePay: 400m)
        };

        var data = Map(orderPays: pays);

        Assert.Equal(2, data.LineItems.Count);
        Assert.All(data.LineItems, line => Assert.Equal(1m, line.Quantity));
        Assert.Equal(375m, data.LineItems[0].UnitPrice);
        Assert.Equal(375m, data.LineItems[0].LineTotal);
        Assert.Equal(400m, data.LineItems[1].LineTotal);
    }

    [Fact]
    public void Line_Item_Total_Excludes_Bonus_And_Deduction_So_The_Lines_Sum_To_The_SubTotal()
    {
        var pays = new[] { PayrollMockFactory.OrderPay(basePay: 500m, bonusPay: 100m, deductionPay: 40m) };
        var invoice = EmployeeInvoice.CreateFromOrderPays("emp-1", "period-1", pays, "currency-1");

        var data = Map(invoice: WithPeriod(invoice), orderPays: pays);

        Assert.Equal(500m, data.LineItems[0].LineTotal);
        Assert.Equal(invoice.SubTotal, data.LineItems.Sum(l => l.LineTotal));
    }

    [Fact]
    public void Amount_Due_Reconciles_To_The_Line_Items_And_To_The_Stored_Total()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 500m, bonusPay: 100m),
            PayrollMockFactory.OrderPay(basePay: 250m, deductionPay: 40m)
        };
        var invoice = EmployeeInvoice.CreateFromOrderPays("emp-1", "period-1", pays, "currency-1");

        var data = Map(invoice: WithPeriod(invoice), orderPays: pays);

        Assert.Equal(
            data.LineItems.Sum(l => l.LineTotal) + data.BonusAmount - data.DeductionAmount,
            data.TotalAmount);
        Assert.Equal(invoice.TotalAmount, data.TotalAmount);
    }

    // ── the VAT branch: the specimen's supplier is not registered ─────

    [Fact]
    public void Cleaner_Without_A_Vat_Number_Is_Not_A_Vat_Payer_And_The_Invoice_Carries_No_Vat()
    {
        var data = Map();

        Assert.False(data.Supplier.IsVatPayer);
        Assert.Null(data.Supplier.VatNumber);
        Assert.Equal(0m, data.VatAmount);
    }

    [Fact]
    public void Cleaner_With_A_Vat_Number_Is_A_Vat_Payer_And_The_Number_Reaches_The_Document()
    {
        var employee = Cleaner();
        employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, "12345678", "CZ12345678", null);

        var data = Map(employee);

        Assert.True(data.Supplier.IsVatPayer);
        Assert.Equal("CZ12345678", data.Supplier.VatNumber);
    }

    // The country's VAT setting is the CUSTOMER-order regime. A zero here has to follow from the
    // supplier being unregistered, not from a literal that happens to agree with it.
    [Fact]
    public void Vat_Stays_Zero_For_An_Unregistered_Cleaner_Even_Where_The_Country_Requires_Vat()
    {
        var invoice = Invoice(subTotal: 1000m);

        var data = Map(invoice: invoice, countryContext: CzechContext());

        Assert.Equal(0m, data.VatAmount);
        Assert.Equal(invoice.TotalAmount, data.TotalAmount);
    }

    // The pay is gross — the cleaner receives the stored total and settles their own taxes — so a
    // registered supplier's VAT comes OUT of that total. Adding it would pay them more than was owed.
    [Fact]
    public void Vat_Is_Carved_Out_Of_The_Stored_Total_When_The_Cleaner_Is_Registered()
    {
        var employee = Cleaner();
        employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, "12345678", "CZ12345678", null);
        var invoice = Invoice(subTotal: 1000m);

        var data = Map(employee, invoice, countryContext: CzechContext());

        Assert.Equal(173.55m, data.VatAmount);
        Assert.Equal(826.45m, data.TotalAmount - data.VatAmount);
    }

    // AC7's identity, and the reason gross is the better answer: it now holds EXACTLY in both variants
    // instead of becoming "stored + VAT" for one of them.
    [Fact]
    public void Printed_Total_Equals_The_Stored_Total_For_A_Vat_Payer_And_A_Non_Payer_Alike()
    {
        var registered = Cleaner();
        registered.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, "12345678", "CZ12345678", null);

        var nonPayerInvoice = Invoice(subTotal: 1000m);
        var payerInvoice = Invoice(subTotal: 1000m);

        Assert.Equal(nonPayerInvoice.TotalAmount, Map(Cleaner(), nonPayerInvoice, countryContext: CzechContext()).TotalAmount);
        Assert.Equal(payerInvoice.TotalAmount, Map(registered, payerInvoice, countryContext: CzechContext()).TotalAmount);
    }

    [Fact]
    public void A_Registered_Cleaner_In_A_Country_That_Requires_No_Vat_Is_Charged_None()
    {
        var employee = Cleaner();
        employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, "12345678", "CZ12345678", null);

        var data = Map(employee, Invoice(subTotal: 1000m), countryContext: new CountryInvoiceContext { VatRequired = false, VatRate = 0.21m });

        Assert.Equal(0m, data.VatAmount);
    }

    // ── whose legal notice reaches the document ──────────────────────

    [Fact]
    public void The_Jurisdictions_Reviewed_Notice_Is_What_Reaches_The_Document()
    {
        var context = CzechContext() with
        {
            LegalDisclaimerTemplate = "Zákonný text.",
            LegalDisclaimerLanguageCode = "cs",
            LegalDisclaimerReviewStatus = LegalNoticeReviewStatus.BusinessSupplied
        };

        Assert.Equal("Zákonný text.", Map(countryContext: context).LegalDisclaimer);
    }

    // The mapper must not hand unreviewed text to the layout: the layout's own fallback is what an
    // unreviewed jurisdiction gets, and it can only choose it if the mapper sends nothing.
    [Fact]
    public void An_Unreviewed_Jurisdictions_Text_Does_Not_Reach_The_Document()
    {
        var context = CzechContext() with
        {
            LegalDisclaimerTemplate = "This invoice is issued in accordance with Czech law.",
            LegalDisclaimerReviewStatus = LegalNoticeReviewStatus.NotReviewed
        };

        Assert.Null(Map(countryContext: context).LegalDisclaimer);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private static InvoicePdfData Map(
        Employee? employee = null,
        EmployeeInvoice? invoice = null,
        IReadOnlyList<OrderEmployeePay>? orderPays = null,
        CountryInvoiceContext? countryContext = null,
        EmployeePayoutDetails? payoutDetails = null)
    {
        return (invoice ?? Invoice()).CreatePdfData(
            employee ?? Cleaner(),
            CurrencyMockFactory.Generate(),
            orderPays ?? [PayrollMockFactory.OrderPay(basePay: 100m)],
            countryContext,
            Company(),
            payoutDetails);
    }

    private static CountryInvoiceContext CzechContext(string? constantSymbol = null) => new()
    {
        VatRequired = true,
        VatRate = 0.21m,
        ConstantSymbol = constantSymbol
    };

    private static EmployeePayoutDetails PayoutDetails(
        string? prefix = null,
        string number = "5885638003",
        string bankCode = "5500") =>
        EmployeePayoutDetails.Create(
            employeeId: PayrollMockFactory.EmployeeId,
            scheme: PayoutScheme.CzskDomesticWithIban,
            bankCountryId: "cz",
            status: PayoutDetailsStatus.Provided,
            accountPrefix: prefix,
            accountNumber: number,
            bankCode: bankCode,
            iban: "CZ3155000000005885638003",
            swift: "RZBCCZPP",
            bankName: "Raiffeisenbank");

    private static EmployeeInvoice Invoice(DateTime? generatedAt = null, decimal subTotal = 100m) =>
        PayrollMockFactory.Invoice(
            subTotal: subTotal,
            generatedAt: generatedAt,
            payPeriod: PayrollMockFactory.OpenPeriod());

    private static EmployeeInvoice WithPeriod(EmployeeInvoice invoice)
    {
        typeof(EmployeeInvoice)
            .GetProperty(nameof(EmployeeInvoice.PayPeriod))!
            .SetValue(invoice, PayrollMockFactory.OpenPeriod());
        return invoice;
    }

    private static Employee Cleaner()
    {
        var user = User.CreateWithPassword("jan.novak@cleansia.test", "Password1", "Jan", "Novák");
        user.UpdatePhoneNumber("+420777123456");

        var address = Address.Create("Dlouhá 12", "Praha", "11000", "cz");
        typeof(Address)
            .GetProperty(nameof(Address.Country))!
            .SetValue(address, Country.Create("Czechia", "CZ"));

        var employee = Employee.CreateWithUser(user);
        employee.UpdateAddress(address);
        employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, "12345678", null, null);
        employee.UpdateBankDetails("CZ3155000000005885638003");
        return employee;
    }

    private static CompanyInfo Company() =>
        CompanyInfo.Create(
            legalName: "Cleansia s.r.o.",
            tradingName: "Cleansia",
            registrationNumber: "87654321",
            street: "Václavské náměstí 1",
            city: "Praha",
            zipCode: "11000",
            countryId: "cz",
            vatNumber: "CZ87654321",
            iban: "CZ1101000000001234567890",
            bankAccountNumber: "1234567890/0100",
            swift: "KOMBCZPP");
}
