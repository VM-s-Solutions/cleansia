using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The single payout write path (ADR-0034 D8). Two things are load-bearing beyond "it saves": it raises
/// <c>Employee.HasPayoutDetails</c> — the profile gate's scalar, which nothing else writes — and it is a
/// create-or-update keyed on the employee, so the cardinality-one index never has to arbitrate.
/// </summary>
public class UpdateBankDetailsTests
{
    private const string EmployeeId = "emp-1";
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string CzCountryId = "country-cz";
    private const string OwnerAccount = "5885638003";
    private const string OwnerIban = "CZ3155000000005885638003";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IEmployeePayoutDetailsRepository> _payoutDetails = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurations = new();
    private readonly Employee _employee;

    public UpdateBankDetailsTests()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        _employee = Employee.CreateWithUser(user);
        _employee.Id = EmployeeId;

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employees.Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>())).ReturnsAsync(_employee);
        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(_employee);
        _employees.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var country = Country.Create("Czechia", "CZE");
        country.Id = CzCountryId;
        _countries.Setup(r => r.GetByIdAsync(CzCountryId, It.IsAny<CancellationToken>())).ReturnsAsync(country);
        _countries.Setup(r => r.GetByIsoCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(country);

        var configuration = CountryConfiguration.Create(CzCountryId, "CZK", "cs", 21m);
        typeof(CountryConfiguration)
            .GetProperty(nameof(CountryConfiguration.PayoutScheme))!
            .SetValue(configuration, PayoutScheme.CzskDomesticWithIban);
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync(CzCountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
    }

    private IPayoutDetailsValidator PayoutValidator() =>
        new PayoutDetailsValidator(_countries.Object, _countryConfigurations.Object);

    private UpdateBankDetails.Validator CreateValidator() =>
        new(_employees.Object, _session.Object, PayoutValidator());

    private UpdateBankDetails.Handler CreateHandler() => new(
        _employees.Object, _session.Object, _payoutDetails.Object, PayoutValidator(),
        NullLogger<UpdateBankDetails.Handler>.Instance);

    private static UpdateBankDetails.Command Valid() => new(
        EmployeeId: EmployeeId,
        Iban: null,
        BankCountryId: CzCountryId,
        AccountNumber: OwnerAccount,
        BankCode: "5500");

    [Fact]
    public async Task Valid_Domestic_Details_Pass_Validation()
    {
        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Gate 0.5 — this exact string is 21 characters and passes <c>Length(15, 34)</c>, which is the whole
    /// of today's server-side validation of a cleaner's bank account.
    /// </summary>
    [Fact]
    public async Task The_TwentyOne_Character_Nonsense_String_No_Longer_Passes()
    {
        var command = Valid() with { AccountNumber = null, BankCode = null, Iban = "totally not an iban!!" };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayoutAccountNumberRequired);
    }

    [Fact]
    public async Task A_Card_Number_Is_Refused_With_Its_Own_Reason_So_The_Client_Can_Say_Why()
    {
        var command = Valid() with { AccountNumber = "4111111111111111" };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayoutLooksLikeCard);
    }

    /// <summary>
    /// The payout destination is written for the JWT caller, whatever id the body names (S1,
    /// [OWN-DATA]). The rule this replaces compared the session employee to the CLIENT-supplied id, so
    /// it only ever passed for a caller that already knew the answer — it refused nobody an attacker
    /// could not impersonate, and refused every client that cannot supply the id at all.
    /// </summary>
    [Fact]
    public async Task Another_Cleaners_Employee_Id_Is_Ignored_Rather_Than_Refused()
    {
        var command = Valid() with { EmployeeId = "emp-2" };
        _employees.Setup(r => r.ExistsAsync("emp-2", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var validation = await CreateValidator().ValidateAsync(command);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(validation.IsValid);
        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeId, result.Value.EmployeeId);
        _employees.Verify(r => r.GetByIdAsync("emp-2", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Command_With_No_Employee_Id_At_All_Writes_The_Callers_Record()
    {
        var command = Valid() with { EmployeeId = null };

        var validation = await CreateValidator().ValidateAsync(command);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(validation.IsValid);
        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeId, result.Value.EmployeeId);
    }

    [Fact]
    public async Task A_Caller_With_No_Employee_Record_Is_Refused()
    {
        _employees
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var validation = await CreateValidator().ValidateAsync(Valid());
        var result = await CreateHandler().Handle(Valid(), CancellationToken.None);

        Assert.Contains(validation.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotAllowedToUpdateEmployee);
        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.EmployeeNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task First_Write_Creates_The_Record_And_Raises_The_Profile_Gate_Scalar()
    {
        EmployeePayoutDetails? added = null;
        _payoutDetails.Setup(r => r.Add(It.IsAny<EmployeePayoutDetails>()))
            .Callback<EmployeePayoutDetails>(d => added = d);

        var result = await CreateHandler().Handle(Valid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(EmployeeId, added!.EmployeeId);
        Assert.Equal(PayoutScheme.CzskDomesticWithIban, added.Scheme);
        Assert.Equal(PayoutDetailsStatus.Provided, added.Status);
        Assert.Equal(OwnerIban, added.Iban);
        Assert.NotNull(added.ConfirmedAt);
        Assert.True(_employee.HasPayoutDetails);
    }

    [Fact]
    public async Task A_Second_Write_Replaces_The_Record_In_Place_Rather_Than_Inserting_A_Second_One()
    {
        var existing = EmployeePayoutDetails.Create(
            EmployeeId, PayoutScheme.CzskDomesticWithIban, CzCountryId, PayoutDetailsStatus.NeedsReconfirmation);
        _payoutDetails
            .Setup(r => r.GetByEmployeeIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Valid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _payoutDetails.Verify(r => r.Add(It.IsAny<EmployeePayoutDetails>()), Times.Never);
        Assert.Equal(OwnerIban, existing.Iban);
        Assert.Equal(PayoutDetailsStatus.Provided, existing.Status);
    }

    [Fact]
    public async Task The_Derived_Iban_Is_Mirrored_Onto_The_Legacy_Column_The_Invoice_Still_Prints()
    {
        await CreateHandler().Handle(Valid(), CancellationToken.None);

        Assert.Equal(OwnerIban, _employee.IBAN);
    }

    [Fact]
    public async Task The_Duplicate_Destination_Check_Compares_The_Derived_Iban_Not_The_Typed_Parts()
    {
        await CreateHandler().Handle(Valid(), CancellationToken.None);

        _payoutDetails.Verify(
            r => r.IsIbanUsedByAnotherEmployeeAsync(OwnerIban, EmployeeId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
