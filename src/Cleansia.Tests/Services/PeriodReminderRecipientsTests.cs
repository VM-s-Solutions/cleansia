using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

public class PeriodReminderRecipientsTests
{
    private const string TenantId = "tenant-a";

    [Fact]
    public async Task The_Period_End_Reminder_Goes_Only_To_Cleaners_With_Work_In_The_Period()
    {
        var period = PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).AddDays(-13));
        period.Id = "period-ending";
        period.TenantId = TenantId;

        var worked = Cleaner("worked");
        var feeShare = Cleaner("fee-share");
        var idle = Cleaner("idle");
        var chargedOnly = Cleaner("charged-only");
        var workedElsewhere = Cleaner("worked-elsewhere");

        var invoicedJob = PayrollMockFactory.OrderPay(employeeId: chargedOnly.Id, payPeriodId: "period-earlier");
        invoicedJob.AssignToInvoice("invoice-earlier");
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(employeeId: worked.Id, payPeriodId: period.Id),
            OrderEmployeePay.CreateFeeShare(
                "order-cancelled", feeShare.Id, period.Id, PayrollMockFactory.CurrencyId,
                PayLineType.CancellationFeeShare, 50m, "{}"),
            invoicedJob.CarryDisputeCharge(period.Id, "dispute-1", 40m, "damage"),
            PayrollMockFactory.OrderPay(employeeId: workedElsewhere.Id, payPeriodId: "period-earlier"),
        };

        var periods = new Mock<IPayPeriodRepository>();
        periods.Setup(r => r.GetQueryableIgnoringTenant()).Returns(new[] { period }.AsQueryable().BuildMock());
        var employees = new Mock<IEmployeeRepository>();
        employees
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { worked, feeShare, idle, chargedOnly, workedElsewhere }.AsQueryable().BuildMock());
        var orderPays = new Mock<IOrderEmployeePayRepository>();
        orderPays.Setup(r => r.GetQueryableIgnoringTenant()).Returns(pays.AsQueryable().BuildMock());

        var recipients = new List<string>();
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(s => s.SendPeriodEndReminderEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string email, string _, DateOnly _, DateOnly _, int _, string _, CancellationToken _) =>
                recipients.Add(email))
            .ReturnsAsync("message-id");

        await new PeriodReminderBackgroundService(
                periods.Object,
                employees.Object,
                orderPays.Object,
                emailService.Object,
                Mock.Of<ITenantProvider>(),
                NullLogger<PeriodReminderBackgroundService>.Instance)
            .SendPeriodEndRemindersAsync(CancellationToken.None);

        Assert.Equal(["fee-share@cleansia.test", "worked@cleansia.test"], recipients.Order());
    }

    private static Employee Cleaner(string name)
    {
        var employee = Employee.CreateWithUser(
            User.CreateWithPassword($"{name}@cleansia.test", "Password1", name, "Cleaner", UserProfile.Employee, "en"));
        employee.Id = $"employee-{name}";
        employee.TenantId = TenantId;
        return employee;
    }
}
