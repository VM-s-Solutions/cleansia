using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The onboarding consent checkbox is a legal record, not a form gate: GDPR Art. 7(1) requires the
/// controller to be able to DEMONSTRATE consent, and a cohort that onboarded without a persisted grant
/// cannot be repaired retroactively. These tests pin that completing onboarding writes the
/// <see cref="UserConsent"/> row through the same grant mechanism the GDPR consent endpoints use.
/// </summary>
public class OnboardingConsentTests
{
    private const string EmployeeId = "emp-1";
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string Ip = "203.0.113.9";
    private const string DeviceLabel = "Chrome/Windows";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IEmployeeDocumentRepository> _employeeDocumentRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly Mock<IAddressGeocoder> _addressGeocoder = new();
    private readonly Mock<IUserConsentRepository> _userConsentRepository = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();

    private readonly Employee _employee;

    public OnboardingConsentTests()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        _employee = Employee.CreateWithUser(user);
        _employee.Id = EmployeeId;

        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_employee);
        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _requestMetadata.SetupGet(m => m.IpAddress).Returns(Ip);
        _requestMetadata.SetupGet(m => m.DeviceLabel).Returns(DeviceLabel);
    }

    private UpdateEmployee.Handler CreateHandler() => new(
        _employeeRepository.Object,
        _employeeDocumentRepository.Object,
        _session.Object,
        _blobClientFactory.Object,
        _addressGeocoder.Object,
        new ConsentService(_requestMetadata.Object, _userConsentRepository.Object));

    private static UpdateEmployee.Command Valid() => new(
        EmployeeId: EmployeeId,
        FirstName: "First",
        LastName: "Last",
        BirthDate: new DateOnly(1990, 1, 1),
        Street: "Main Street 10",
        City: "Prague",
        ZipCode: "11000",
        CountryId: "cz",
        State: null,
        NationalityId: "cz",
        Phone: "+420123456789",
        PassportId: "AB12345",
        EntityType: EmployeeEntityType.NaturalPerson,
        RegistrationNumber: "12345678",
        VatNumber: null,
        LegalEntityName: null,
        EmergencyName: null,
        EmergencyPhone: null,
        Consent: true);

    [Fact]
    public async Task Completing_Onboarding_Persists_A_DataProcessing_Consent_Grant()
    {
        UserConsent? added = null;
        _userConsentRepository.Setup(r => r.Add(It.IsAny<UserConsent>())).Callback<UserConsent>(c => added = c);

        var result = await CreateHandler().Handle(Valid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(_employee.UserId, added!.UserId);
        Assert.Equal(ConsentType.DataProcessing, added.ConsentType);
        Assert.True(added.IsGranted);
        Assert.NotNull(added.GrantedAt);
    }

    [Fact]
    public async Task The_Persisted_Grant_Carries_The_Server_Observed_Ip_And_Device()
    {
        UserConsent? added = null;
        _userConsentRepository.Setup(r => r.Add(It.IsAny<UserConsent>())).Callback<UserConsent>(c => added = c);

        await CreateHandler().Handle(Valid(), CancellationToken.None);

        Assert.Equal(Ip, added!.IpAddress);
        Assert.Equal(DeviceLabel, added.UserAgent);
    }

    [Fact]
    public async Task Re_Submitting_Onboarding_Does_Not_Write_A_Second_Grant()
    {
        var existing = UserConsent.Grant(_employee.UserId, ConsentType.DataProcessing, "198.51.100.1", "Firefox");
        _userConsentRepository
            .Setup(r => r.GetByUserAndTypeAsync(_employee.UserId, ConsentType.DataProcessing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Valid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userConsentRepository.Verify(r => r.Add(It.IsAny<UserConsent>()), Times.Never);
        Assert.Equal("198.51.100.1", existing.IpAddress);
    }

    [Fact]
    public async Task A_Withdrawn_Consent_Is_Regranted_Rather_Than_Duplicated()
    {
        var withdrawn = UserConsent
            .Grant(_employee.UserId, ConsentType.DataProcessing, "198.51.100.1", "Firefox")
            .Withdraw();
        _userConsentRepository
            .Setup(r => r.GetByUserAndTypeAsync(_employee.UserId, ConsentType.DataProcessing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(withdrawn);

        await CreateHandler().Handle(Valid(), CancellationToken.None);

        _userConsentRepository.Verify(r => r.Add(It.IsAny<UserConsent>()), Times.Never);
        Assert.True(withdrawn.IsGranted);
        Assert.Null(withdrawn.WithdrawnAt);
        Assert.Equal(Ip, withdrawn.IpAddress);
    }
}
