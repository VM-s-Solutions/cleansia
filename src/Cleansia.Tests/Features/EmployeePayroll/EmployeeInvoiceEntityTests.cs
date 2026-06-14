using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Pure-entity nets for <see cref="EmployeeInvoice"/>: clamp-to-zero, invoice numbering and
/// payment-reference shape, deterministic variable symbol, and the status-transition guards that
/// protect the money path (a paid invoice is terminal).
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
            bonusAmount: 10.01m,
            deductionAmount: 5.50m);

        Assert.Equal(204.50m, invoice.TotalAmount);
        Assert.Equal(199.99m, invoice.SubTotal);
        Assert.Equal(10.01m, invoice.BonusAmount);
        Assert.Equal(5.50m, invoice.DeductionAmount);
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

    // ── AC10: deterministic variable symbol ──────────────────────────

    [Fact]
    public void GenerateVariableSymbol_Is_Deterministic_For_Same_Inputs()
    {
        var first = EmployeeInvoice.GenerateVariableSymbol("emp-1", "period-1");
        var second = EmployeeInvoice.GenerateVariableSymbol("emp-1", "period-1");

        Assert.Equal(first, second);
    }

    [Fact]
    public void GenerateVariableSymbol_Is_Ten_Numeric_Characters()
    {
        var symbol = EmployeeInvoice.GenerateVariableSymbol("emp-1", "period-1");

        Assert.Equal(10, symbol.Length);
        Assert.Matches(@"^\d{10}$", symbol);
    }

    [Fact]
    public void GenerateVariableSymbol_Differs_Across_Periods_For_Same_Employee()
    {
        var first = EmployeeInvoice.GenerateVariableSymbol("emp-1", "period-1");
        var second = EmployeeInvoice.GenerateVariableSymbol("emp-1", "period-2");

        Assert.NotEqual(first, second);
    }

    // ── cross-invocation (cross-process) determinism ────────────────
    // The expected values are independently derived from the stable FNV-1a-32 basis over the
    // UTF-8 input bytes (empHash = fnv1a32("emp-1") % 10000, periodHash = fnv1a32("period-1") %
    // 1000000, formatted D4+D6). They are hard-coded, not a second in-process call, so this would
    // FAIL against a per-process string.GetHashCode() basis.

    [Theory]
    [InlineData("emp-1", "period-1", "1883454606")]
    [InlineData("emp-1", "period-2", "1883676987")]
    public void GenerateVariableSymbol_Matches_Stable_Hash_Expected_Value(
        string employeeId, string payPeriodId, string expected)
    {
        var symbol = EmployeeInvoice.GenerateVariableSymbol(employeeId, payPeriodId);

        Assert.Equal(expected, symbol);
    }

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
