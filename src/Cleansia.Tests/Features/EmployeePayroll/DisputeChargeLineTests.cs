using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// A dispute charge on a job already invoiced becomes a line of its own in the open period, and the next
/// invoice deducts it whole, oldest first, only while the invoice stays at or above zero. A charge that
/// does not fit waits for a later invoice (owner ruling 2026-11-09).
/// </summary>
public class DisputeChargeLineTests
{
    private const string OpenPeriodId = "period-open";

    [Fact]
    public void The_Charge_Line_Copies_The_Job_And_Holds_The_Charge_As_A_Negative_Total()
    {
        var job = InvoicedJob(600m);

        var line = job.CarryDisputeCharge(OpenPeriodId, "dispute-1", 150m, "Bathroom left uncleaned");

        Assert.Equal((job.OrderId, job.EmployeeId, job.CurrencyId), (line.OrderId, line.EmployeeId, line.CurrencyId));
        Assert.Equal(OpenPeriodId, line.PayPeriodId);
        Assert.Equal(PayLineType.DisputeCharge, line.LineType);
        Assert.Equal((0m, 150m, -150m), (line.BasePay, line.DeductionPay, line.TotalPay));
        Assert.Equal(("dispute-1", "Bathroom left uncleaned"), (line.DeductionDisputeId, line.DeductionReason));
        Assert.Null(line.EmployeeInvoiceId);
        Assert.Equal((600m, 0m, "invoice-1"), (job.TotalPay, job.DeductionPay, job.EmployeeInvoiceId));
        Assert.Null(job.DeductionDisputeId);
    }

    [Fact]
    public void A_Charge_Above_The_Jobs_Pay_Is_Not_Carried()
    {
        var job = InvoicedJob(600m);

        Assert.False(job.CanTakeDisputeCharge(650m));
        Assert.Throws<InvalidOperationException>(() => job.CarryDisputeCharge(OpenPeriodId, "dispute-1", 650m, "Late"));
    }

    [Fact]
    public void An_Invoiced_Job_Takes_A_Charge_Only_As_A_Carried_Line()
    {
        var job = InvoicedJob(600m);

        Assert.True(job.CanTakeDisputeCharge(150m));
        Assert.Throws<InvalidOperationException>(() => job.ChargeForDispute("dispute-1", 150m, "Late"));
        Assert.Equal(600m, job.TotalPay);
    }

    [Fact]
    public void Only_An_Uninvoiced_Charge_Line_Moves_To_Another_Period()
    {
        var line = InvoicedJob(600m).CarryDisputeCharge(OpenPeriodId, "dispute-1", 150m, "Late");

        line.MoveToPeriod("period-next");
        Assert.Equal("period-next", line.PayPeriodId);

        line.AssignToInvoice("invoice-2");
        Assert.Throws<InvalidOperationException>(() => line.MoveToPeriod("period-later"));
        Assert.Throws<InvalidOperationException>(() => PayrollMockFactory.OrderPay(basePay: 600m).MoveToPeriod("period-next"));
    }

    [Fact]
    public void An_Invoice_Over_Pay_And_A_Charge_Deducts_The_Charge_From_The_Pay()
    {
        var rows = new[] { PayrollMockFactory.OrderPay(basePay: 800m), Charge(150m) };

        var invoice = EmployeeInvoice.CreateFromOrderPays(
            PayrollMockFactory.EmployeeId, PayrollMockFactory.PayPeriodId, rows,
            PayrollMockFactory.TestVariableSymbol, PayrollMockFactory.TestInvoiceNumber);

        Assert.Equal((800m, 0m, 150m, 650m), (invoice.SubTotal, invoice.BonusAmount, invoice.DeductionAmount, invoice.TotalAmount));
        Assert.Equal(rows.Sum(r => r.TotalPay), invoice.TotalAmount);
        Assert.Equal(2, invoice.TotalOrders);
    }

    [Fact]
    public void An_Invoice_Whose_Rows_Net_Below_Zero_Is_Refused_Rather_Than_Floored()
    {
        var rows = new[] { PayrollMockFactory.OrderPay(basePay: 100m), Charge(150m) };

        Assert.Throws<InvalidOperationException>(() => EmployeeInvoice.CreateFromOrderPays(
            PayrollMockFactory.EmployeeId, PayrollMockFactory.PayPeriodId, rows,
            PayrollMockFactory.TestVariableSymbol, PayrollMockFactory.TestInvoiceNumber));
    }

    [Fact]
    public void A_Charge_Larger_Than_The_Pay_Waits_And_The_Pay_Is_Invoiced_In_Full()
    {
        var pay = PayrollMockFactory.OrderPay(basePay: 100m);
        var charge = Charge(150m);

        var (invoiced, waiting) = EmployeeInvoice.FitDisputeCharges([pay, charge]);

        Assert.Equal([pay], invoiced);
        Assert.Equal([charge], waiting);
    }

    [Fact]
    public void Charges_Are_Taken_Whole_Oldest_First_Each_One_That_Still_Fits()
    {
        var pay = PayrollMockFactory.OrderPay(basePay: 1000m);
        var first = Charge(600m, minutesAgo: 30);
        var second = Charge(700m, minutesAgo: 20);
        var third = Charge(100m, minutesAgo: 10);

        var (invoiced, waiting) = EmployeeInvoice.FitDisputeCharges([third, pay, second, first]);

        Assert.Equal(300m, invoiced.Sum(r => r.TotalPay));
        Assert.Equal([pay, first, third], invoiced);
        Assert.Equal([second], waiting);
    }

    [Fact]
    public void A_Charge_With_No_Pay_Beside_It_Waits_And_Nothing_Is_Invoiced()
    {
        var charge = Charge(150m);

        var (invoiced, waiting) = EmployeeInvoice.FitDisputeCharges([charge]);

        Assert.Empty(invoiced);
        Assert.Equal([charge], waiting);
    }

    private static OrderEmployeePay InvoicedJob(decimal totalPay)
    {
        var job = PayrollMockFactory.OrderPay(basePay: totalPay);
        job.AssignToInvoice("invoice-1");
        return job;
    }

    private static OrderEmployeePay Charge(decimal amount, int minutesAgo = 0)
    {
        var line = InvoicedJob(amount).CarryDisputeCharge(PayrollMockFactory.PayPeriodId, $"dispute-{Guid.NewGuid():N}", amount, "Late");
        line.Created("admin", DateTimeOffset.UtcNow.AddMinutes(-minutesAgo));
        return line;
    }
}
