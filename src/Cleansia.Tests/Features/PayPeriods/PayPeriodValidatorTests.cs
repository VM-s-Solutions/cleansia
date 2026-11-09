using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Services;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// Characterization of the PayPeriod create/update validators: pins every BusinessErrorMessage
/// code each validator emits (valid input passes; each rule fires) so the B3 base-class composition
/// refactor stays behavior-preserving. The shared session-email-confirmation rule (inherited via
/// UserEmailValidator) is part of the pinned contract.
/// </summary>
public class PayPeriodValidatorTests
{
    private const string UserEmail = "admin@cleansia.cz";
    private const string PayPeriodId = PayrollMockFactory.PayPeriodId;

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();

    private void ArrangeConfirmedSession(bool confirmed = true)
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "A", "D");
        if (confirmed)
        {
            user.ConfirmEmail();
        }

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _userRepository
            .Setup(r => r.GetByEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 1, 14);

    // 14 Jan 2026 is in the past on the real clock, so every period rule reads this one.
    private readonly StubTimeProvider _clock = new(new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero));

    // ── CreatePayPeriod ──────────────────────────────────────────────

    private CreatePayPeriod.Validator CreateValidator(int? lengthSetting = null) => new(
        _userRepository.Object,
        _session.Object,
        _payPeriodRepository.Object,
        PayPeriodTestServices.ClosingConfig(lengthSetting),
        _clock);

    private CreatePayPeriod.Command ValidCreate() => new(Start, Notes: null);

    [Fact]
    public async Task Create_Valid_Command_Passes()
    {
        ArrangeConfirmedSession();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, End, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(ValidCreate());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Create_Unconfirmed_Email_Fails_NotExistingUserWithEmail()
    {
        ArrangeConfirmedSession(confirmed: false);
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, End, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(ValidCreate());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotExistingUserWithEmail);
    }

    /// <summary>
    /// The server sets the end, start plus the configured length, so the overlap check covers that span.
    /// </summary>
    [Theory]
    [InlineData(null, 14)]
    [InlineData(3, 3)]
    public async Task Create_Checks_Overlap_Over_The_Configured_Length(int? lengthSetting, int days)
    {
        ArrangeConfirmedSession();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, Start.AddDays(days - 1), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateValidator(lengthSetting).ValidateAsync(ValidCreate());

        Assert.Equal(BusinessErrorMessage.OverlappingPeriod, Assert.Single(result.Errors).ErrorMessage);
    }

    /// <summary>
    /// One open period per company: pay joins the open one, so a second would only split it. The open period
    /// overlaps the new one too, and only one refusal may come back: two whole-command errors share one code,
    /// and the client cannot translate them joined.
    /// </summary>
    [Fact]
    public async Task Create_While_A_Period_Is_Open_Fails_CompanyHasOpenPayPeriod()
    {
        ArrangeConfirmedSession();
        _payPeriodRepository.Setup(r => r.ExistsActivePeriodAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, End, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateValidator().ValidateAsync(ValidCreate());

        Assert.Equal(BusinessErrorMessage.CompanyHasOpenPayPeriod, Assert.Single(result.Errors).ErrorMessage);
    }

    /// <summary>A period that starts later would still take today's pay, since pay joins the open period.</summary>
    [Theory]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public async Task Create_Refuses_A_Start_After_Today(int day, bool valid)
    {
        ArrangeConfirmedSession();
        var start = new DateOnly(2026, 1, day);

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { StartDate = start });

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal(nameof(CreatePayPeriod.Command.StartDate), error.PropertyName);
            Assert.Equal(BusinessErrorMessage.InvalidDate, error.ErrorMessage);
        }
    }

    [Fact]
    public async Task Create_Overlapping_Period_Fails_OverlappingPeriod()
    {
        ArrangeConfirmedSession();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, End, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateValidator().ValidateAsync(ValidCreate());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OverlappingPeriod);
    }

    [Fact]
    public async Task Create_Notes_Too_Long_Fails_MaxLength()
    {
        ArrangeConfirmedSession();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, End, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { Notes = new string('x', 501) });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreatePayPeriod.Command.Notes)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }

    // ── UpdatePayPeriod ──────────────────────────────────────────────

    private UpdatePayPeriod.Validator UpdateValidator(int? lengthSetting = null) => new(
        _userRepository.Object,
        _session.Object,
        _payPeriodRepository.Object,
        PayPeriodTestServices.ClosingConfig(lengthSetting),
        _clock);

    private UpdatePayPeriod.Command ValidUpdate() => new(PayPeriodId, Start, End, Notes: null);

    private void ArrangeOpenPeriod(DateOnly? start = null)
    {
        _payPeriodRepository.Setup(r => r.ExistsAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payPeriodRepository
            .Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayrollMockFactory.OpenPeriod(start));
    }

    [Fact]
    public async Task Update_Valid_Command_Passes()
    {
        ArrangeConfirmedSession();
        ArrangeOpenPeriod();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, End, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Update_Unconfirmed_Email_Fails_NotExistingUserWithEmail()
    {
        ArrangeConfirmedSession(confirmed: false);
        ArrangeOpenPeriod();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, End, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotExistingUserWithEmail);
    }

    [Fact]
    public async Task Update_Empty_Id_Fails_Required()
    {
        ArrangeConfirmedSession();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate() with { PayPeriodId = string.Empty });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdatePayPeriod.Command.PayPeriodId)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task Update_Unknown_Period_Fails_PayPeriodNotFound()
    {
        ArrangeConfirmedSession();
        _payPeriodRepository.Setup(r => r.ExistsAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, End, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdatePayPeriod.Command.PayPeriodId)
            && e.ErrorMessage == BusinessErrorMessage.PayPeriodNotFound);
    }

    [Fact]
    public async Task Update_Closed_Period_Fails_PayPeriodNotOpen()
    {
        ArrangeConfirmedSession();
        _payPeriodRepository.Setup(r => r.ExistsAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payPeriodRepository
            .Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayrollMockFactory.ClosedPeriod());
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, End, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdatePayPeriod.Command.PayPeriodId)
            && e.ErrorMessage == BusinessErrorMessage.PayPeriodNotOpen);
    }

    /// <summary>
    /// Change end date moves only the end, to any day from tomorrow to the period's last allowed day. Ending it today
    /// or earlier is Close now's job, so the two actions never overlap.
    /// </summary>
    [Theory]
    [InlineData(6, null)]
    [InlineData(14, null)]
    [InlineData(5, BusinessErrorMessage.PayPeriodEndTooSoon)]
    [InlineData(4, BusinessErrorMessage.PayPeriodEndTooSoon)]
    [InlineData(15, BusinessErrorMessage.InvalidDuration)]
    public async Task Update_Takes_An_End_From_Tomorrow_To_The_Last_Allowed_Day(int day, string? error)
    {
        ArrangeConfirmedSession();
        ArrangeOpenPeriod();

        var result = await UpdateValidator().ValidateAsync(ValidUpdate() with { EndDate = new DateOnly(2026, 1, day) });

        if (error is null)
        {
            Assert.True(result.IsValid);
            return;
        }

        var failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(UpdatePayPeriod.Command.EndDate), failure.PropertyName);
        Assert.Equal(error, failure.ErrorMessage);
    }

    [Fact]
    public async Task Update_Moving_The_Start_Fails_InvalidDate()
    {
        ArrangeConfirmedSession();
        ArrangeOpenPeriod();

        var result = await UpdateValidator().ValidateAsync(ValidUpdate() with { StartDate = new DateOnly(2026, 1, 2) });

        var failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(UpdatePayPeriod.Command.PayPeriodId), failure.PropertyName);
        Assert.Equal(BusinessErrorMessage.InvalidDate, failure.ErrorMessage);
    }

    /// <summary>The period Close now opens starts tomorrow, so its earliest end is the day after its start.</summary>
    [Fact]
    public async Task Update_Of_A_Period_Starting_Tomorrow_Refuses_An_End_On_Its_First_Day()
    {
        var tomorrow = new DateOnly(2026, 1, 6);
        ArrangeConfirmedSession();
        ArrangeOpenPeriod(tomorrow);

        var result = await UpdateValidator().ValidateAsync(new UpdatePayPeriod.Command(PayPeriodId, tomorrow, tomorrow, Notes: null));

        Assert.Equal(BusinessErrorMessage.InvalidDate, Assert.Single(result.Errors).ErrorMessage);
        Assert.True((await UpdateValidator().ValidateAsync(
            new UpdatePayPeriod.Command(PayPeriodId, tomorrow, tomorrow.AddDays(1), Notes: null))).IsValid);
    }

    [Theory]
    [InlineData(3, null)]
    [InlineData(4, BusinessErrorMessage.InvalidDuration)]
    public async Task Update_With_A_Three_Day_Setting_Takes_The_Third_Day_And_Refuses_A_Fourth(int days, string? error)
    {
        var start = new DateOnly(2026, 1, 4);
        ArrangeConfirmedSession();
        ArrangeOpenPeriod(start);

        var result = await UpdateValidator(lengthSetting: 3).ValidateAsync(
            new UpdatePayPeriod.Command(PayPeriodId, start, start.AddDays(days - 1), Notes: null));

        Assert.Equal(error is null, result.IsValid);
        if (error is not null)
        {
            Assert.Equal(error, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Fact]
    public async Task Update_Overlapping_Period_Fails_OverlappingPeriod()
    {
        ArrangeConfirmedSession();
        ArrangeOpenPeriod();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(Start, End, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OverlappingPeriod);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
