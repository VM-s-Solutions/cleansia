using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// "Datum splatnosti" on the payout invoice. The due date is DERIVED from the persisted issue date
/// rather than stored, so regenerating the PDF of an already-issued invoice can never move it.
/// </summary>
public class EmployeeInvoiceDueDateTests
{
    [Fact]
    public void CalculateDueDate_Is_The_Issue_Date_Plus_The_Payment_Terms()
    {
        var invoice = InvoiceIssuedOn(new DateTime(2026, 3, 2, 9, 15, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 3, 16), invoice.CalculateDueDate(14));
    }

    [Fact]
    public void CalculateDueDate_Ignores_The_Issue_Time_Of_Day()
    {
        var invoice = InvoiceIssuedOn(new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 4, 14), invoice.CalculateDueDate(14));
    }

    [Fact]
    public void CalculateDueDate_Is_Stable_Across_Regeneration()
    {
        var invoice = InvoiceIssuedOn(new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(invoice.CalculateDueDate(14), invoice.CalculateDueDate(14));
    }

    [Fact]
    public void CalculateDueDate_Rejects_Negative_Payment_Terms()
    {
        var invoice = PayrollMockFactory.Invoice();

        Assert.Throws<ArgumentOutOfRangeException>(() => invoice.CalculateDueDate(-1));
    }

    private static EmployeeInvoice InvoiceIssuedOn(DateTime issuedAt)
    {
        var invoice = PayrollMockFactory.Invoice();
        typeof(EmployeeInvoice)
            .GetProperty(nameof(EmployeeInvoice.GeneratedAt))!
            .SetValue(invoice, issuedAt);
        return invoice;
    }
}
