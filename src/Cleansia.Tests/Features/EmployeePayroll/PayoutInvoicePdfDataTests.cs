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
        var data = Map();

        Assert.Equal("CZ3155000000005885638003", data.Supplier.Iban);
        Assert.NotEqual(data.Company!.Iban, data.Supplier.Iban);
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

    // ── arrangement ──────────────────────────────────────────────────

    private static InvoicePdfData Map(
        Employee? employee = null,
        EmployeeInvoice? invoice = null,
        IReadOnlyList<OrderEmployeePay>? orderPays = null,
        CountryInvoiceContext? countryContext = null)
    {
        return (invoice ?? Invoice()).CreatePdfData(
            employee ?? Cleaner(),
            CurrencyMockFactory.Generate(),
            orderPays ?? [PayrollMockFactory.OrderPay(basePay: 100m)],
            countryContext,
            Company());
    }

    private static EmployeeInvoice Invoice(DateTime? generatedAt = null) =>
        PayrollMockFactory.Invoice(
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
