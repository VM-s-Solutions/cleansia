using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Marking an invoice paid tells the cleaner "you've been paid" (T-0431) — the highest-value payroll
/// signal, and one they heard nothing about before. One feed row + push to the invoice's own
/// employee, skipping a legacy invoice with no linked user; the notify rides the command's unit of
/// work via the producer seam (no commit here).
/// </summary>
public class MarkInvoicePaidNotifyTests
{
    private readonly Mock<IEmployeeInvoiceRepository> _invoices = new();
    private readonly Mock<INotificationProducer> _producer = new();

    private MarkInvoicePaid.Handler Handler() => new(_invoices.Object, _producer.Object);

    private static EmployeeInvoice ApprovedInvoice(string? employeeUserId)
    {
        var invoice = EmployeeInvoice.Create(
            employeeId: "emp-1",
            payPeriodId: "pp-1",
            totalOrders: 3,
            subTotal: 900m,
            currencyId: "cur-czk");
        invoice.Approve("admin-1");

        if (employeeUserId is not null)
        {
            var user = User.CreateWithPassword($"{employeeUserId}@cleansia.test", "Passw0rd!", "Clean", "Er");
            user.Id = employeeUserId;
            var employee = Employee.CreateWithUser(user);
            typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.Employee))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(invoice, [employee]);
        }
        return invoice;
    }

    [Fact]
    public async Task Marking_An_Invoice_Paid_Notifies_Its_Employee_With_The_Invoice_Id()
    {
        var invoice = ApprovedInvoice("emp-user-1");
        _invoices.Setup(r => r.GetByIdAsync(invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);

        var result = await Handler().Handle(
            new MarkInvoicePaid.Command(invoice.Id, BankTransferNote: null, AdminNotes: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _producer.Verify(p => p.NotifyAsync(
            "emp-user-1",
            NotificationEventCatalog.InvoicePaid,
            It.Is<Dictionary<string, string>>(d => d["invoiceId"] == invoice.Id),
            invoice.TenantId,
            invoice.Id,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task An_Invoice_With_No_Linked_User_Notifies_No_One()
    {
        var invoice = ApprovedInvoice(employeeUserId: null);
        _invoices.Setup(r => r.GetByIdAsync(invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);

        var result = await Handler().Handle(
            new MarkInvoicePaid.Command(invoice.Id, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _producer.VerifyNoOtherCalls();
    }
}
