using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;

public static class PayrollMockFactory
{
    public const string EmployeeId = "emp-1";
    public const string PayPeriodId = "period-1";
    public const string CurrencyId = "currency-1";

    public static OrderEmployeePay OrderPay(
        decimal basePay = 100m,
        decimal extrasPay = 0m,
        decimal expensesPay = 0m,
        decimal bonusPay = 0m,
        decimal deductionPay = 0m,
        string? orderId = null,
        string employeeId = EmployeeId,
        string payPeriodId = PayPeriodId)
    {
        var totalPay = basePay + extrasPay + expensesPay + bonusPay - deductionPay;
        if (totalPay < 0)
        {
            totalPay = 0;
        }

        var pay = OrderEmployeePay.Create(
            orderId: orderId ?? $"order-{Guid.NewGuid():N}",
            employeeId: employeeId,
            payPeriodId: payPeriodId,
            basePay: basePay,
            extrasPay: extrasPay,
            expensesPay: expensesPay,
            bonusPay: bonusPay,
            deductionPay: deductionPay,
            totalPay: totalPay);
        pay.Id = $"oep-{Guid.NewGuid():N}";
        return pay;
    }

    public static EmployeeInvoice Invoice(
        int totalOrders = 1,
        decimal subTotal = 100m,
        decimal bonusAmount = 0m,
        decimal deductionAmount = 0m,
        string employeeId = EmployeeId,
        string payPeriodId = PayPeriodId,
        string currencyId = CurrencyId)
    {
        var invoice = EmployeeInvoice.Create(
            employeeId: employeeId,
            payPeriodId: payPeriodId,
            totalOrders: totalOrders,
            subTotal: subTotal,
            currencyId: currencyId,
            bonusAmount: bonusAmount,
            deductionAmount: deductionAmount);
        invoice.Id = $"inv-{Guid.NewGuid():N}";
        return invoice;
    }

    public static PayPeriod OpenPeriod(DateOnly? startDate = null)
    {
        var period = PayPeriod.CreateBiWeekly(startDate ?? new DateOnly(2026, 1, 1));
        period.Id = PayPeriodId;
        return period;
    }

    public static PayPeriod ClosedPeriod(string closedBy = "admin@cleansia.cz")
    {
        var period = OpenPeriod();
        period.Close(closedBy);
        return period;
    }

    public static PayPeriod PaidPeriod()
    {
        var period = ClosedPeriod();
        period.MarkAsPaid();
        return period;
    }
}
