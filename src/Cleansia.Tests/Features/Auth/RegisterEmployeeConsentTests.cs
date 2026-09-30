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
/// A cleaner registering is never recorded as accepting the customer terms or privacy policy, ticked or
/// not: those are the customer's texts, and a cleaner's own documents are accepted through
/// <c>AcceptLegalDocument</c>. Employee registration is not gated on the tick and writes no customer
/// audit row.
/// </summary>
public sealed class RegisterEmployeeConsentTests
{
    private const string Email = "new.cleaner@example.com";
    private const string Password = "Password1!@abc";
    private const string Language = "cs";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
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
    }

    private RegisterEmployee.Handler CreateHandler() =>
        new(_userRepository.Object, _employeeRepository.Object, _pending);

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
    public void The_Handler_Holds_No_Consent_Writer_So_No_Tick_Grants_A_Customer_Text()
    {
        Assert.DoesNotContain(
            typeof(RegisterEmployee.Handler).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(IConsentService));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task A_Ticked_Or_Unticked_Box_Registers(bool? termsAccepted)
    {
        var result = await CreateHandler().Handle(Command(termsAccepted), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_createdUser);
    }

    [Fact]
    public async Task A_Re_Registration_Of_An_Unconfirmed_Account_Upgrades_The_Existing_User()
    {
        var existing = User.CreateWithPassword(Email, Password, "John", "Doe", UserProfile.Customer, Language);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        Assert.Equal(UserProfile.Employee, existing.Profile);
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
