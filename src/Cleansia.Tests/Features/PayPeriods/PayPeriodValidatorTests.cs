using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
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
    private static readonly DateOnly End = new(2026, 1, 15);

    // ── CreatePayPeriod ──────────────────────────────────────────────

    private CreatePayPeriod.Validator CreateValidator() => new(
        _userRepository.Object,
        _session.Object,
        _payPeriodRepository.Object);

    private CreatePayPeriod.Command ValidCreate() => new(Start, End, Notes: null);

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

    [Fact]
    public async Task Create_End_Before_Start_Fails_InvalidDate()
    {
        ArrangeConfirmedSession();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { EndDate = Start.AddDays(-1) });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreatePayPeriod.Command.EndDate)
            && e.ErrorMessage == BusinessErrorMessage.InvalidDate);
    }

    [Fact]
    public async Task Create_Duration_Too_Short_Fails_InvalidDuration()
    {
        ArrangeConfirmedSession();
        _payPeriodRepository
            .Setup(r => r.HasOverlappingPeriodAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { EndDate = Start.AddDays(3) });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreatePayPeriod.Command.EndDate)
            && e.ErrorMessage == BusinessErrorMessage.InvalidDuration);
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

    private UpdatePayPeriod.Validator UpdateValidator() => new(
        _userRepository.Object,
        _session.Object,
        _payPeriodRepository.Object);

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
