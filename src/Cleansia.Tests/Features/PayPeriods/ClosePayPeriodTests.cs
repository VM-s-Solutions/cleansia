using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// The close-pay-period money gate. The handler stamps the closing admin from the session
///; the validator refuses an unknown period, a non-Open period, a period with any non-Paid
/// invoice, and over-long notes. Asserts on the BusinessErrorMessage constant.
/// </summary>
public class ClosePayPeriodTests
{
    private const string PayPeriodId = PayrollMockFactory.PayPeriodId;
    private const string AdminEmail = "admin@cleansia.cz";

    // ── Handler (AC14) ───────────────────────────────────────────────

    [Fact]
    public async Task Handler_Closes_Open_Period_Stamping_Session_Email()
    {
        var period = PayrollMockFactory.OpenPeriod();
        var payPeriodRepository = new Mock<IPayPeriodRepository>();
        payPeriodRepository
            .Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserEmail()).Returns(AdminEmail);
        var handler = new ClosePayPeriod.Handler(payPeriodRepository.Object, session.Object);

        var result = await handler.Handle(
            new ClosePayPeriod.Command(PayPeriodId, Notes: "period reconciled"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PayPeriodStatus.Closed, period.Status);
        Assert.NotNull(period.ClosedAt);
        Assert.Equal(AdminEmail, period.ClosedBy);
    }

    // ── Validator (AC15) ─────────────────────────────────────────────

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();

    private void ArrangeValidSession()
    {
        var confirmedUser = User.CreateWithPassword(AdminEmail, "Password1", "A", "D");
        confirmedUser.ConfirmEmail();
        _session.Setup(s => s.GetUserEmail()).Returns(AdminEmail);
        _userRepository
            .Setup(r => r.GetByEmailAsync(AdminEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmedUser);
    }

    private void ArrangePeriod(PayPeriod? period, bool exists = true)
    {
        _payPeriodRepository
            .Setup(r => r.ExistsAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(exists);
        _payPeriodRepository
            .Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(period);
    }

    private void ArrangeAllInvoicesPaid(bool allPaid) =>
        _invoiceRepository
            .Setup(r => r.AllInvoicesPaidInPeriodAsync(PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPaid);

    private ClosePayPeriod.Validator CreateValidator() => new(
        _userRepository.Object,
        _session.Object,
        _payPeriodRepository.Object,
        _invoiceRepository.Object);

    private static ClosePayPeriod.Command Valid() => new(PayPeriodId, Notes: null);

    [Fact]
    public async Task Valid_Command_Passes()
    {
        ArrangeValidSession();
        ArrangePeriod(PayrollMockFactory.OpenPeriod());
        ArrangeAllInvoicesPaid(true);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Unknown_Period_Fails_PayPeriodNotFound()
    {
        ArrangeValidSession();
        ArrangePeriod(period: null, exists: false);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ClosePayPeriod.Command.PayPeriodId)
            && e.ErrorMessage == BusinessErrorMessage.PayPeriodNotFound);
    }

    [Fact]
    public async Task Closed_Period_Fails_PayPeriodNotOpen()
    {
        ArrangeValidSession();
        ArrangePeriod(PayrollMockFactory.ClosedPeriod());
        ArrangeAllInvoicesPaid(true);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ClosePayPeriod.Command.PayPeriodId)
            && e.ErrorMessage == BusinessErrorMessage.PayPeriodNotOpen);
    }

    [Fact]
    public async Task Unpaid_Invoice_In_Period_Fails_UnpaidInvoicesExist()
    {
        ArrangeValidSession();
        ArrangePeriod(PayrollMockFactory.OpenPeriod());
        ArrangeAllInvoicesPaid(false);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ClosePayPeriod.Command.PayPeriodId)
            && e.ErrorMessage == BusinessErrorMessage.UnpaidInvoicesExist);
    }

    [Fact]
    public async Task Notes_Over_1000_Chars_Fails_MaxLength()
    {
        ArrangeValidSession();
        ArrangePeriod(PayrollMockFactory.OpenPeriod());
        ArrangeAllInvoicesPaid(true);

        var result = await CreateValidator().ValidateAsync(Valid() with { Notes = new string('x', 1001) });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ClosePayPeriod.Command.Notes)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }
}
