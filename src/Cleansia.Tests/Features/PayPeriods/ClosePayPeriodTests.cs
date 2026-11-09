using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Services;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// Close now, the exception to the night close (owner ruling of 8 November 2026): an admin ends the open period
/// today with a reason the audit log keeps, and the next period opens tomorrow, so pay always has an open period.
/// The clock stands at 5 January 2026 and the open period runs from 1 to 14 January.
/// </summary>
public class ClosePayPeriodTests
{
    private const string PayPeriodId = PayrollMockFactory.PayPeriodId;
    private const string AdminEmail = "admin@cleansia.cz";

    private readonly StubTimeProvider _clock = new(new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero));

    // ── Handler ──────────────────────────────────────────────────────

    private readonly Mock<IAuditContext> _audit = new();
    private readonly List<PayPeriod> _added = [];

    private ClosePayPeriod.Handler Handler(PayPeriod period, int? lengthSetting = null, params PayPeriod[] others)
    {
        var repository = new Mock<IPayPeriodRepository>();
        repository.Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.GetQueryable()).Returns(() => others.Prepend(period).AsQueryable().BuildMock());
        repository.Setup(r => r.Add(It.IsAny<PayPeriod>())).Callback<PayPeriod>(_added.Add);
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserEmail()).Returns(AdminEmail);
        return new ClosePayPeriod.Handler(
            repository.Object, session.Object, _audit.Object, PayPeriodTestServices.ClosingConfig(lengthSetting), _clock);
    }

    [Fact]
    public async Task Handler_Ends_The_Period_Today_Stamps_The_Admin_Audits_The_Reason_And_Opens_The_Next_Tomorrow()
    {
        var period = PayrollMockFactory.OpenPeriod();
        object? before = null;
        object? after = null;
        string? reason = null;
        _audit.Setup(a => a.RecordChange("PayPeriod", PayPeriodId, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string?>()))
            .Callback<string, string, object, object, string?>((_, _, b, a, r) => (before, after, reason) = (b, a, r));

        var result = await Handler(period).Handle(
            new ClosePayPeriod.Command(PayPeriodId, "  A cleaner left mid-period  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PayPeriodStatus.Closed, period.Status);
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5)), (period.StartDate, period.EndDate));
        Assert.Equal(AdminEmail, period.ClosedBy);
        Assert.NotNull(period.ClosedAt);
        Assert.Null(period.Notes);

        var next = Assert.Single(_added);
        Assert.Equal((new DateOnly(2026, 1, 6), new DateOnly(2026, 1, 19), PayPeriodStatus.Open), (next.StartDate, next.EndDate, next.Status));

        _audit.Verify(a => a.RecordChange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string?>()),
            Times.Once);
        Assert.Equal(new ClosePayPeriod.PeriodSnapshot(PayPeriodId, new DateOnly(2026, 1, 14), PayPeriodStatus.Open), before);
        Assert.Equal(new ClosePayPeriod.PeriodSnapshot(PayPeriodId, new DateOnly(2026, 1, 5), PayPeriodStatus.Closed), after);
        Assert.Equal("A cleaner left mid-period", reason);
    }

    [Fact]
    public async Task Handler_With_Another_Period_Open_Adds_No_Successor()
    {
        var other = PayPeriod.CreateBiWeekly(new DateOnly(2026, 1, 15));

        var result = await Handler(PayrollMockFactory.OpenPeriod(), others: [other])
            .Handle(new ClosePayPeriod.Command(PayPeriodId, "duplicate period"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_added);
    }

    [Fact]
    public async Task Handler_Opens_A_Successor_Of_The_Configured_Length()
    {
        var result = await Handler(PayrollMockFactory.OpenPeriod(), lengthSetting: 3)
            .Handle(new ClosePayPeriod.Command(PayPeriodId, "DEV cycle"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var next = Assert.Single(_added);
        Assert.Equal((new DateOnly(2026, 1, 6), new DateOnly(2026, 1, 8)), (next.StartDate, next.EndDate));
    }

    /// <summary>
    /// Between midnight and the 02:00 UTC run a period can be past its end and still open. Close now keeps that end,
    /// and the next period starts the day after it, as the night run's would, so no day falls between the two.
    /// </summary>
    [Fact]
    public async Task Handler_On_A_Period_Past_Its_End_Keeps_The_End_And_Starts_The_Next_The_Day_After()
    {
        var period = PayrollMockFactory.OpenPeriod(new DateOnly(2025, 12, 21));

        var result = await Handler(period).Handle(new ClosePayPeriod.Command(PayPeriodId, "night run off"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 1, 3), period.EndDate);
        var next = Assert.Single(_added);
        Assert.Equal((new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 17)), (next.StartDate, next.EndDate));
    }

    // ── Validator ────────────────────────────────────────────────────

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();

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

    private ClosePayPeriod.Validator CreateValidator() => new(
        _userRepository.Object,
        _session.Object,
        _payPeriodRepository.Object,
        _clock);

    private static ClosePayPeriod.Command Valid() => new(PayPeriodId, Reason: "A cleaner left mid-period");

    [Fact]
    public async Task Valid_Command_Passes()
    {
        ArrangeValidSession();
        ArrangePeriod(PayrollMockFactory.OpenPeriod());

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

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ClosePayPeriod.Command.PayPeriodId)
            && e.ErrorMessage == BusinessErrorMessage.PayPeriodNotOpen);
    }

    /// <summary>The period Close now opens starts tomorrow; closing it before then would leave pay no open period.</summary>
    [Theory]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public async Task A_Period_That_Has_Not_Started_Fails_PayPeriodNotStarted(int startDay, bool valid)
    {
        ArrangeValidSession();
        ArrangePeriod(PayrollMockFactory.OpenPeriod(new DateOnly(2026, 1, startDay)));

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal(nameof(ClosePayPeriod.Command.PayPeriodId), error.PropertyName);
            Assert.Equal(BusinessErrorMessage.PayPeriodNotStarted, error.ErrorMessage);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_Blank_Reason_Fails_Required(string? reason)
    {
        ArrangeValidSession();
        ArrangePeriod(PayrollMockFactory.OpenPeriod());

        var result = await CreateValidator().ValidateAsync(Valid() with { Reason = reason! });

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ClosePayPeriod.Command.Reason), error.PropertyName);
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
    }

    [Fact]
    public async Task A_Reason_Over_500_Characters_Fails_MaxLength()
    {
        ArrangeValidSession();
        ArrangePeriod(PayrollMockFactory.OpenPeriod());

        var result = await CreateValidator().ValidateAsync(Valid() with { Reason = new string('x', 501) });

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ClosePayPeriod.Command.Reason), error.PropertyName);
        Assert.Equal(BusinessErrorMessage.MaxLength, error.ErrorMessage);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
