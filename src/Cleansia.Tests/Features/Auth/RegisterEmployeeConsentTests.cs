using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Azure.Storage.Queues;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// When a client sends the terms tick with the employee registration, the handler writes the terms
/// and privacy consents server-side on the user it created or reused — with NO legal document: an
/// employee accepts a different text than the customer documents (ADR-0041), so the row stays
/// unversioned exactly as the <c>GrantConsent</c> route leaves it. Employee registration is not gated
/// on the tick and writes no customer audit row.
/// </summary>
public sealed class RegisterEmployeeConsentTests
{
    private const string Email = "new.cleaner@example.com";
    private const string Password = "Password1!@abc";
    private const string Language = "cs";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IConsentService> _consentService = new();
    private readonly IPendingDispatch _pending = new InMemoryPendingDispatch();
    private User? _createdUser;

    public RegisterEmployeeConsentTests()
    {
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.Add(It.IsAny<User>()))
            .Callback<User>(user => _createdUser = user);
        _consentService
            .Setup(s => s.TryGrantAsync(It.IsAny<string>(), It.IsAny<ConsentType>(), It.IsAny<LegalDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private RegisterEmployee.Handler CreateHandler() =>
        new(_userRepository.Object, _employeeRepository.Object, _pending, _consentService.Object);

    private static RegisterEmployee.Command Command(bool? termsAccepted) =>
        new(Email, Password, "John", "Doe", Language, TermsAccepted: termsAccepted);

    [Fact]
    public void RegisterEmployee_Carries_No_Customer_Marker()
    {
        var descriptor = AuditActionDescriptor.For(typeof(RegisterEmployee.Command));

        Assert.Equal(AuditAudience.Admin, descriptor.Audience);
        Assert.False(descriptor.AllowsAnonymousActor);
    }

    [Fact]
    public async Task An_Asserted_Tick_Grants_Both_Legal_Consents_On_The_New_User_With_No_Document()
    {
        var result = await CreateHandler().Handle(Command(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_createdUser);
        _consentService.Verify(
            s => s.TryGrantAsync(_createdUser!.Id, ConsentType.TermsOfService, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _consentService.Verify(
            s => s.TryGrantAsync(_createdUser!.Id, ConsentType.PrivacyPolicy, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _consentService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task A_Missing_Or_Unticked_Box_Grants_Nothing_And_Still_Registers(bool? termsAccepted)
    {
        var result = await CreateHandler().Handle(Command(termsAccepted), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_createdUser);
        _consentService.VerifyNoOtherCalls();
    }

    // The unconfirmed re-registration upgrades the existing row; a tick on that attempt is a grant on
    // that user (the service makes it a no-op when the row already holds it).
    [Fact]
    public async Task A_Re_Registration_Of_An_Unconfirmed_Account_Grants_On_The_Existing_User()
    {
        var existing = User.CreateWithPassword(Email, Password, "John", "Doe", UserProfile.Customer, Language);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        Assert.Equal(UserProfile.Employee, existing.Profile);
        _consentService.Verify(
            s => s.TryGrantAsync(existing.Id, ConsentType.TermsOfService, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _consentService.Verify(
            s => s.TryGrantAsync(existing.Id, ConsentType.PrivacyPolicy, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _consentService.VerifyNoOtherCalls();
    }

    // Employee registration is not gated on the tick: the validator has no rule for it, so a client
    // that sends nothing (or false) is accepted and simply holds no consent.
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task The_Validator_Does_Not_Gate_On_The_Tick(bool? termsAccepted)
    {
        var languages = new Mock<ILanguageRepository>();
        languages.Setup(r => r.ExistsWithCodeAsync(Language, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new RegisterEmployee.Validator(_userRepository.Object, languages.Object, Mock.Of<ITenantProvider>());

        var result = await validator.ValidateAsync(Command(termsAccepted));

        Assert.True(result.IsValid);
    }
}
