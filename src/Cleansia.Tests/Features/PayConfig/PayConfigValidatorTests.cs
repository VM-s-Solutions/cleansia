using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.PayConfig;

/// <summary>
/// Characterization of the PayConfig create/update validators: pins every BusinessErrorMessage
/// code the validators emit (valid input passes; each rule fires) so the B3 base-class composition
/// refactor stays behavior-preserving. The shared session-email-confirmation rule (inherited via
/// UserEmailValidator) is part of the contract pinned here.
/// </summary>
public class PayConfigValidatorTests
{
    private const string UserEmail = "admin@cleansia.cz";
    private const string ServiceId = "svc-1";
    private const string PackageId = "pkg-1";
    private const string CurrencyId = "cur-1";
    private const string EmployeeId = "emp-1";
    private const string PayConfigId = "pc-1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();

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

    // ── CreatePayConfig ──────────────────────────────────────────────

    private CreatePayConfig.Validator CreateValidator() => new(
        _userRepository.Object,
        _session.Object,
        _serviceRepository.Object,
        _packageRepository.Object,
        _currencyRepository.Object,
        _payConfigRepository.Object,
        _employeeRepository.Object);

    private CreatePayConfig.Command ValidCreate() => new(
        EmployeeId: null,
        ServiceId: ServiceId,
        PackageId: null,
        BasePay: 100m,
        ExtraPerRoom: 0m,
        ExtraPerBathroom: 0m,
        DistanceRatePerKm: 0m,
        MinimumPay: 0m,
        MaximumPay: 0m,
        CurrencyId: CurrencyId,
        Description: null);

    private void ArrangeValidCreateDeps()
    {
        _serviceRepository.Setup(r => r.ExistsAsync(ServiceId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _currencyRepository.Setup(r => r.ExistsAsync(CurrencyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payConfigRepository
            .Setup(r => r.GetByServiceIdAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Core.Domain.EmployeePayroll.EmployeePayConfig?)null);
    }

    [Fact]
    public async Task Create_Valid_Command_Passes()
    {
        ArrangeConfirmedSession();
        ArrangeValidCreateDeps();

        var result = await CreateValidator().ValidateAsync(ValidCreate());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Create_Unconfirmed_Email_Fails_NotExistingUserWithEmail()
    {
        ArrangeConfirmedSession(confirmed: false);
        ArrangeValidCreateDeps();

        var result = await CreateValidator().ValidateAsync(ValidCreate());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotExistingUserWithEmail);
    }

    [Fact]
    public async Task Create_No_Service_And_No_Package_Fails_ServiceOrPackageRequired()
    {
        ArrangeConfirmedSession();
        _currencyRepository.Setup(r => r.ExistsAsync(CurrencyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { ServiceId = null, PackageId = null });

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayConfigServiceOrPackageRequired);
    }

    [Fact]
    public async Task Create_Both_Service_And_Package_Fails_CannotHaveBoth()
    {
        ArrangeConfirmedSession();
        _currencyRepository.Setup(r => r.ExistsAsync(CurrencyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { ServiceId = ServiceId, PackageId = PackageId });

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayConfigCannotHaveBoth);
    }

    [Fact]
    public async Task Create_Unknown_Employee_Fails_EmployeeNotFound()
    {
        ArrangeConfirmedSession();
        ArrangeValidCreateDeps();
        _employeeRepository.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { EmployeeId = EmployeeId });

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotFound);
    }

    [Fact]
    public async Task Create_Unknown_Service_Fails_NotFound()
    {
        ArrangeConfirmedSession();
        _currencyRepository.Setup(r => r.ExistsAsync(CurrencyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _serviceRepository.Setup(r => r.ExistsAsync(ServiceId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(ValidCreate());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreatePayConfig.Command.ServiceId)
            && e.ErrorMessage == BusinessErrorMessage.NotFound);
    }

    [Fact]
    public async Task Create_Existing_Service_PayConfig_Fails_AlreadyExists()
    {
        ArrangeConfirmedSession();
        _currencyRepository.Setup(r => r.ExistsAsync(CurrencyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _serviceRepository.Setup(r => r.ExistsAsync(ServiceId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payConfigRepository
            .Setup(r => r.GetByServiceIdAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Core.Domain.EmployeePayroll.EmployeePayConfig.CreateForService(
                ServiceId, 50m, CurrencyId, 0m, 0m, 0m, null, null));

        var result = await CreateValidator().ValidateAsync(ValidCreate());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreatePayConfig.Command.ServiceId)
            && e.ErrorMessage == BusinessErrorMessage.PayConfigAlreadyExists);
    }

    [Fact]
    public async Task Create_Negative_BasePay_Fails_BasePayNegative()
    {
        ArrangeConfirmedSession();
        ArrangeValidCreateDeps();

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { BasePay = -1m });

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayConfigBasePayNegative);
    }

    [Fact]
    public async Task Create_Max_Less_Than_Min_Fails_MaximumLessThanMinimum()
    {
        ArrangeConfirmedSession();
        ArrangeValidCreateDeps();

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { MinimumPay = 50m, MaximumPay = 10m });

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayConfigMaximumLessThanMinimum);
    }

    [Fact]
    public async Task Create_Empty_Currency_Fails_Required()
    {
        ArrangeConfirmedSession();
        _serviceRepository.Setup(r => r.ExistsAsync(ServiceId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payConfigRepository
            .Setup(r => r.GetByServiceIdAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Core.Domain.EmployeePayroll.EmployeePayConfig?)null);

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { CurrencyId = string.Empty });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreatePayConfig.Command.CurrencyId)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task Create_Unknown_Currency_Fails_InvalidCurrency()
    {
        ArrangeConfirmedSession();
        _serviceRepository.Setup(r => r.ExistsAsync(ServiceId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _currencyRepository.Setup(r => r.ExistsAsync(CurrencyId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _payConfigRepository
            .Setup(r => r.GetByServiceIdAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Core.Domain.EmployeePayroll.EmployeePayConfig?)null);

        var result = await CreateValidator().ValidateAsync(ValidCreate());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
    }

    [Fact]
    public async Task Create_Description_Too_Long_Fails_MaxLength()
    {
        ArrangeConfirmedSession();
        ArrangeValidCreateDeps();

        var result = await CreateValidator().ValidateAsync(ValidCreate() with { Description = new string('x', 501) });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreatePayConfig.Command.Description)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }

    // ── UpdatePayConfig ──────────────────────────────────────────────

    private UpdatePayConfig.Validator UpdateValidator() => new(
        _userRepository.Object,
        _session.Object,
        _payConfigRepository.Object);

    private UpdatePayConfig.Command ValidUpdate() => new(
        PayConfigId: PayConfigId,
        BasePay: 100m,
        ExtraPerRoom: 0m,
        ExtraPerBathroom: 0m,
        DistanceRatePerKm: 0m,
        MinimumPay: 0m,
        MaximumPay: 0m,
        Description: null);

    [Fact]
    public async Task Update_Valid_Command_Passes()
    {
        ArrangeConfirmedSession();
        _payConfigRepository.Setup(r => r.ExistsAsync(PayConfigId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Update_Unconfirmed_Email_Fails_NotExistingUserWithEmail()
    {
        ArrangeConfirmedSession(confirmed: false);
        _payConfigRepository.Setup(r => r.ExistsAsync(PayConfigId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotExistingUserWithEmail);
    }

    [Fact]
    public async Task Update_Empty_Id_Fails_Required()
    {
        ArrangeConfirmedSession();

        var result = await UpdateValidator().ValidateAsync(ValidUpdate() with { PayConfigId = string.Empty });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdatePayConfig.Command.PayConfigId)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task Update_Unknown_PayConfig_Fails_PayConfigNotFound()
    {
        ArrangeConfirmedSession();
        _payConfigRepository.Setup(r => r.ExistsAsync(PayConfigId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdatePayConfig.Command.PayConfigId)
            && e.ErrorMessage == BusinessErrorMessage.PayConfigNotFound);
    }

    [Fact]
    public async Task Update_Negative_BasePay_Fails_BasePayNegative()
    {
        ArrangeConfirmedSession();
        _payConfigRepository.Setup(r => r.ExistsAsync(PayConfigId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate() with { BasePay = -1m });

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayConfigBasePayNegative);
    }

    [Fact]
    public async Task Update_Max_Less_Than_Min_Fails_MaximumLessThanMinimum()
    {
        ArrangeConfirmedSession();
        _payConfigRepository.Setup(r => r.ExistsAsync(PayConfigId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await UpdateValidator().ValidateAsync(ValidUpdate() with { MinimumPay = 50m, MaximumPay = 10m });

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayConfigMaximumLessThanMinimum);
    }
}
