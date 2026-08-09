using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Pure-entity nets for <see cref="EmployeeInvoice"/>: clamp-to-zero, invoice numbering and
/// payment-reference shape, the stamped-at-construction variable symbol and its one-time
/// assignment, and the status-transition guards that protect the money path (a paid invoice is
/// terminal).
/// </summary>
public class EmployeeInvoiceEntityTests
{
    // ── AC3: negative total clamps to zero ───────────────────────────

    [Fact]
    public void Create_Clamps_Negative_Total_To_Zero()
    {
        var invoice = EmployeeInvoice.Create(
            employeeId: "emp-1",
            payPeriodId: "period-1",
            totalOrders: 1,
            subTotal: 100m,
            currencyId: "currency-1",
            variableSymbol: PayrollMockFactory.TestVariableSymbol,
            bonusAmount: 0m,
            deductionAmount: 500m);

        Assert.Equal(0m, invoice.TotalAmount);
    }

    [Fact]
    public void Create_Computes_Total_From_SubTotal_Bonus_Deduction_Exactly()
    {
        var invoice = EmployeeInvoice.Create(
            employeeId: "emp-1",
            payPeriodId: "period-1",
            totalOrders: 3,
            subTotal: 199.99m,
            currencyId: "currency-1",
            variableSymbol: PayrollMockFactory.TestVariableSymbol,
            bonusAmount: 10.01m,
            deductionAmount: 5.50m);

        Assert.Equal(204.50m, invoice.TotalAmount);
        Assert.Equal(199.99m, invoice.SubTotal);
        Assert.Equal(10.01m, invoice.BonusAmount);
        Assert.Equal(5.50m, invoice.DeductionAmount);
    }

    // ── invoice-from-pays formula: bills Σ TotalPay, no double count ──

    [Fact]
    public void CreateFromOrderPays_Bills_Sum_Of_TotalPay_Without_Double_Counting_Bonus_Deduction()
    {
        // TotalPay already folds bonus/deduction in (100 + 20 - 5 = 115). The invoice must show
        // the bonus/deduction lines AND still total 115, not add them again on top.
        var pays = new[] { PayrollMockFactory.OrderPay(basePay: 100m, bonusPay: 20m, deductionPay: 5m) };

        var invoice = EmployeeInvoice.CreateFromOrderPays("emp-1", "period-1", pays, "currency-1", PayrollMockFactory.TestVariableSymbol);

        Assert.Equal(100m, invoice.SubTotal);
        Assert.Equal(20m, invoice.BonusAmount);
        Assert.Equal(5m, invoice.DeductionAmount);
        Assert.Equal(pays.Sum(p => p.TotalPay), invoice.TotalAmount);
        Assert.Equal(115m, invoice.TotalAmount);
    }

    [Fact]
    public void CreateFromOrderPays_And_AddOrderPays_Agree_On_The_Same_Pays()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 100m, extrasPay: 15m, bonusPay: 20m, deductionPay: 5m),
            PayrollMockFactory.OrderPay(basePay: 250m, expensesPay: 12.50m)
        };

        var created = EmployeeInvoice.CreateFromOrderPays("emp-1", "period-1", pays, "currency-1", PayrollMockFactory.TestVariableSymbol);
        var added = PayrollMockFactory.Invoice(totalOrders: 0, subTotal: 0m).AddOrderPays(pays);

        Assert.Equal(created.SubTotal, added.SubTotal);
        Assert.Equal(created.BonusAmount, added.BonusAmount);
        Assert.Equal(created.DeductionAmount, added.DeductionAmount);
        Assert.Equal(created.TotalAmount, added.TotalAmount);
    }

    // ── AC9: invoice number shape, payment reference, uniqueness ─────

    [Fact]
    public void Create_Sets_InvoiceNumber_To_Inv_YearMonth_Suffix_Shape()
    {
        var invoice = PayrollMockFactory.Invoice();

        Assert.Matches(@"^INV-\d{6}-[0-9A-F]{5}$", invoice.InvoiceNumber);
    }

    [Fact]
    public void Create_Sets_PaymentReference_Equal_To_InvoiceNumber()
    {
        var invoice = PayrollMockFactory.Invoice();

        Assert.Equal(invoice.InvoiceNumber, invoice.PaymentReference);
    }

    [Fact]
    public void Two_Invoices_Created_In_Same_Call_Get_Distinct_InvoiceNumbers()
    {
        var first = PayrollMockFactory.Invoice();
        var second = PayrollMockFactory.Invoice();

        Assert.NotEqual(first.InvoiceNumber, second.InvoiceNumber);
    }

    [Fact]
    public void Create_Starts_Pending()
    {
        var invoice = PayrollMockFactory.Invoice();

        Assert.Equal(EmployeeInvoiceStatus.Pending, invoice.Status);
    }

    // ── the variable symbol is stamped at construction, never derived ─

    [Fact]
    public void Create_Stamps_The_Supplied_Variable_Symbol()
    {
        var invoice = PayrollMockFactory.Invoice(variableSymbol: "2026000042");

        Assert.Equal("2026000042", invoice.VariableSymbol);
    }

    [Fact]
    public void AssignVariableSymbol_Refuses_An_Invoice_That_Already_Carries_One()
    {
        var invoice = PayrollMockFactory.Invoice();

        Assert.Throws<InvalidOperationException>(() => invoice.AssignVariableSymbol("2026000099"));
    }

    [Fact]
    public void AssignVariableSymbol_Refuses_A_Paid_Invoice()
    {
        var invoice = PaidInvoice();
        ClearVariableSymbol(invoice);

        Assert.Throws<InvalidOperationException>(() => invoice.AssignVariableSymbol("2026000099"));
    }

    [Fact]
    public void AssignVariableSymbol_Refuses_A_Cancelled_Invoice()
    {
        var invoice = PayrollMockFactory.Invoice();
        invoice.Cancel("duplicate", "admin@cleansia.cz");
        ClearVariableSymbol(invoice);

        Assert.Throws<InvalidOperationException>(() => invoice.AssignVariableSymbol("2026000099"));
    }

    [Fact]
    public void AssignVariableSymbol_Stamps_A_Symbol_Less_Unpaid_Invoice()
    {
        var invoice = PayrollMockFactory.Invoice();
        ClearVariableSymbol(invoice);

        invoice.AssignVariableSymbol("2026000099");

        Assert.Equal("2026000099", invoice.VariableSymbol);
    }

    // The legacy population this models cannot be built through Create any more — the parameter is
    // required — so a null symbol is reached the only way it exists in production: a row that
    // predates the allocator.
    private static void ClearVariableSymbol(EmployeeInvoice invoice) =>
        typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.VariableSymbol))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(invoice, [null]);

    // ── AC11: Approve legal from Pending/Disputed, illegal elsewhere ─

    [Fact]
    public void Approve_From_Pending_Sets_Approved_And_Audit()
    {
        var invoice = PayrollMockFactory.Invoice();

        invoice.Approve("admin@cleansia.cz", "looks good");

        Assert.Equal(EmployeeInvoiceStatus.Approved, invoice.Status);
        Assert.NotNull(invoice.ApprovedAt);
        Assert.Equal("admin@cleansia.cz", invoice.ApprovedBy);
        Assert.Equal("looks good", invoice.AdminNotes);
    }

    [Fact]
    public void Approve_From_Disputed_Succeeds()
    {
        var invoice = PayrollMockFactory.Invoice();
        invoice.Dispute("under review");

        invoice.Approve("admin@cleansia.cz");

        Assert.Equal(EmployeeInvoiceStatus.Approved, invoice.Status);
    }

    [Theory]
    [InlineData(EmployeeInvoiceStatus.Approved)]
    [InlineData(EmployeeInvoiceStatus.Paid)]
    [InlineData(EmployeeInvoiceStatus.Rejected)]
    [InlineData(EmployeeInvoiceStatus.Cancelled)]
    public void Approve_From_Illegal_Status_Throws(EmployeeInvoiceStatus from)
    {
        var invoice = InvoiceInStatus(from);

        Assert.Throws<InvalidOperationException>(() => invoice.Approve("admin@cleansia.cz"));
    }

    // ── AC12: MarkAsPaid legal only from Approved ────────────────────

    [Fact]
    public void MarkAsPaid_From_Approved_Sets_Paid_And_PaidAt()
    {
        var invoice = PayrollMockFactory.Invoice();
        invoice.Approve("admin@cleansia.cz");

        invoice.MarkAsPaid("bank ref 123");

        Assert.Equal(EmployeeInvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAt);
        Assert.Equal("bank ref 123", invoice.BankTransferNote);
    }

    [Theory]
    [InlineData(EmployeeInvoiceStatus.Pending)]
    [InlineData(EmployeeInvoiceStatus.Disputed)]
    [InlineData(EmployeeInvoiceStatus.Rejected)]
    [InlineData(EmployeeInvoiceStatus.Cancelled)]
    [InlineData(EmployeeInvoiceStatus.Paid)]
    public void MarkAsPaid_From_Non_Approved_Status_Throws(EmployeeInvoiceStatus from)
    {
        var invoice = InvoiceInStatus(from);

        Assert.Throws<InvalidOperationException>(() => invoice.MarkAsPaid());
    }

    // ── AC13: Paid is terminal for Dispute/Reject/UpdateAmounts/Cancel

    [Fact]
    public void Dispute_On_Paid_Throws()
    {
        var invoice = PaidInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.Dispute("x"));
    }

    [Fact]
    public void Reject_On_Paid_Throws()
    {
        var invoice = PaidInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.Reject("x"));
    }

    [Fact]
    public void UpdateAmounts_On_Paid_Throws()
    {
        var invoice = PaidInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.UpdateAmounts(10m, 0m));
    }

    [Fact]
    public void Cancel_On_Paid_Throws()
    {
        var invoice = PaidInvoice();

        Assert.Throws<InvalidOperationException>(() => invoice.Cancel("reason", "admin@cleansia.cz"));
    }

    [Fact]
    public void Cancel_When_Already_Cancelled_Throws()
    {
        var invoice = PayrollMockFactory.Invoice();
        invoice.Cancel("first reason", "admin@cleansia.cz");

        Assert.Throws<InvalidOperationException>(() => invoice.Cancel("again", "admin@cleansia.cz"));
    }

    [Fact]
    public void Cancel_From_Pending_Sets_Cancelled_State()
    {
        var invoice = PayrollMockFactory.Invoice();

        invoice.Cancel("duplicate", "admin@cleansia.cz");

        Assert.True(invoice.IsCancelled);
        Assert.Equal(EmployeeInvoiceStatus.Cancelled, invoice.Status);
        Assert.Equal("duplicate", invoice.CancellationReason);
        Assert.Equal("admin@cleansia.cz", invoice.CancelledBy);
        Assert.NotNull(invoice.CancelledAt);
    }

    [Fact]
    public void UpdateAmounts_On_Pending_Clamps_Negative_Total_To_Zero()
    {
        var invoice = PayrollMockFactory.Invoice(subTotal: 100m);

        invoice.UpdateAmounts(bonusAmount: 0m, deductionAmount: 500m);

        Assert.Equal(0m, invoice.TotalAmount);
    }

    private static EmployeeInvoice PaidInvoice()
    {
        var invoice = PayrollMockFactory.Invoice();
        invoice.Approve("admin@cleansia.cz");
        invoice.MarkAsPaid();
        return invoice;
    }

    private static EmployeeInvoice InvoiceInStatus(EmployeeInvoiceStatus status)
    {
        var invoice = PayrollMockFactory.Invoice();
        switch (status)
        {
            case EmployeeInvoiceStatus.Pending:
                break;
            case EmployeeInvoiceStatus.Approved:
                invoice.Approve("admin@cleansia.cz");
                break;
            case EmployeeInvoiceStatus.Paid:
                invoice.Approve("admin@cleansia.cz");
                invoice.MarkAsPaid();
                break;
            case EmployeeInvoiceStatus.Disputed:
                invoice.Dispute("x");
                break;
            case EmployeeInvoiceStatus.Rejected:
                invoice.Reject("x");
                break;
            case EmployeeInvoiceStatus.Cancelled:
                invoice.Cancel("x", "admin@cleansia.cz");
                break;
        }

        return invoice;
    }
}
