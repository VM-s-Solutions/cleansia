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

    // ── CreatePayPeriod ──────────────────────────────────────────────

    private CreatePayPeriod.Validator CreateValidator(int? lengthSetting = null) => new(
        _userRepository.Object,
        _session.Object,
        _payPeriodRepository.Object,
        PayPeriodTestServices.ClosingConfig(lengthSetting));

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
        PayPeriodTestServices.ClosingConfig(lengthSetting));

    private UpdatePayPeriod.Command ValidUpdate() => new(PayPeriodId, Start, End, Notes: null);

    private void ArrangeOpenPeriod()
    {
        _payPeriodRepository.Setup(r => r.ExistsAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payPeriodRepository
            .Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayrollMockFactory.OpenPeriod());
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

    [Theory]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(15)]
    [InlineData(31)]
    public async Task Update_To_A_Period_Not_Of_14_Days_Fails_InvalidDuration(int days)
    {
        ArrangeConfirmedSession();
        ArrangeOpenPeriod();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate() with { EndDate = Start.AddDays(days - 1) });

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(UpdatePayPeriod.Command.EndDate), error.PropertyName);
        Assert.Equal(BusinessErrorMessage.InvalidDuration, error.ErrorMessage);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task Update_With_A_Three_Day_Setting_Takes_Three_Days_And_Refuses_A_Fourth(int days, bool valid)
    {
        ArrangeConfirmedSession();
        ArrangeOpenPeriod();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await UpdateValidator(lengthSetting: 3).ValidateAsync(ValidUpdate() with { EndDate = Start.AddDays(days - 1) });

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(BusinessErrorMessage.InvalidDuration, Assert.Single(result.Errors).ErrorMessage);
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
}
