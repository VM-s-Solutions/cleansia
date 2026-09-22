using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Azure.Storage.Queues;
using Cleansia.Tests.Domain.Legal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// ADR-0062 D4 — the e-mail registration writes the terms and privacy consents server-side, stamped
/// with the documents in force for the market the visitor registers with, when the client asserted the
/// tick, and records the act as <c>customer.account.register</c> keyed on the NEW user (the session has
/// none yet). The validator is the gate — a registration without the tick never reaches this handler
/// (<c>RegisterValidatorTests</c>) — so the handler records what it is given and refuses nothing itself.
/// The evidence names the method, language, whether a referral code was present and the two
/// versions — never the person.
/// </summary>
public sealed class RegisterConsentAndEvidenceTests
{
    private const string Email = "new.user@example.com";
    private const string Password = "Password1!@abc";
    private const string Language = "cs";
    private const string Market = "country-cze";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ICartRepository> _cartRepository = new();
    private readonly Mock<IConsentService> _consentService = new();
    private readonly IPendingDispatch _pending = new InMemoryPendingDispatch();
    private readonly AuditContext _auditContext = new();
    private readonly LegalDocument _terms = LegalDocumentFixtures.Terms();
    private readonly LegalDocument _privacy = LegalDocumentFixtures.Privacy();
    private readonly Mock<ILegalDocumentResolver> _legalDocuments;
    private User? _createdUser;

    public RegisterConsentAndEvidenceTests()
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
        _legalDocuments = LegalDocumentFixtures.Resolver(_terms, _privacy);
    }

    private Register.Handler CreateHandler() =>
        new(_cartRepository.Object, _userRepository.Object, new Mock<IReferralService>().Object, _pending,
            _consentService.Object, _legalDocuments.Object, _auditContext, NullLogger<Register.Handler>.Instance);

    private static Register.Command Command(bool? termsAccepted, string? referralCode = null, string? countryId = Market) =>
        new(Email, Password, "John", "Doe", Language, ReferralCode: referralCode, CountryId: countryId, TermsAccepted: termsAccepted);

    private JsonElement Payload() => JsonDocument.Parse(_auditContext.DrainSnapshot()!.AfterJson!).RootElement;

    [Fact]
    public void Register_Carries_The_Frozen_Customer_Marker_That_Admits_An_Anonymous_Actor()
    {
        var descriptor = AuditActionDescriptor.For(typeof(Register.Command));

        Assert.Equal("customer.account.register", descriptor.Action);
        Assert.Equal("User", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.True(descriptor.AllowsAnonymousActor);
    }

    /// <summary>
    /// The social commands are marked as the SIGN-IN, never as a registration: a social registration's
    /// proof stays the two consent rows the provisioning branch writes (the handler declines the session
    /// row on that branch).
    /// </summary>
    [Theory]
    [InlineData(typeof(GoogleAuth.Command))]
    [InlineData(typeof(AppleAuth.Command))]
    public void The_Social_SignIn_Or_Register_Commands_Are_Marked_As_A_SignIn_Not_A_Registration(Type commandType)
    {
        var descriptor = AuditActionDescriptor.For(commandType);

        Assert.Equal("customer.session.login", descriptor.Action);
        Assert.NotEqual("customer.account.register", descriptor.Action);
    }

    [Fact]
    public async Task An_Asserted_Tick_Grants_Both_Legal_Consents_Under_The_Documents_In_Force_For_The_Market()
    {
        var result = await CreateHandler().Handle(Command(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_createdUser);
        _legalDocuments.Verify(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, Market, It.IsAny<CancellationToken>()), Times.Once);
        _legalDocuments.Verify(r => r.ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, Market, It.IsAny<CancellationToken>()), Times.Once);
        _consentService.Verify(
            s => s.TryGrantAsync(_createdUser!.Id, ConsentType.TermsOfService, _terms, It.IsAny<CancellationToken>()),
            Times.Once);
        _consentService.Verify(
            s => s.TryGrantAsync(_createdUser!.Id, ConsentType.PrivacyPolicy, _privacy, It.IsAny<CancellationToken>()),
            Times.Once);
        _consentService.VerifyNoOtherCalls();
    }

    // A visitor naming no market gets the default market's texts — the resolver's null means exactly that.
    [Fact]
    public async Task A_Registration_Without_A_Market_Resolves_The_Default_Markets_Documents()
    {
        var result = await CreateHandler().Handle(Command(termsAccepted: true, countryId: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _legalDocuments.Verify(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task The_Handler_Grants_Nothing_For_A_Missing_Or_Unticked_Box_And_Leaves_The_Refusal_To_The_Validator(bool? termsAccepted)
    {
        var result = await CreateHandler().Handle(Command(termsAccepted), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_createdUser);
        _consentService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task The_Evidence_Is_Keyed_On_The_New_User_And_Names_The_Act_Not_The_Person()
    {
        var result = await CreateHandler().Handle(Command(termsAccepted: true, referralCode: "FRIEND-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(_createdUser!.Id, snapshot.ResourceId);
        Assert.Equal(_createdUser.Id, snapshot.ActorUserId);
        Assert.Null(snapshot.BeforeJson);

        var payload = JsonDocument.Parse(snapshot.AfterJson!).RootElement;
        Assert.Equal("Email", payload.GetProperty("method").GetString());
        Assert.Equal(Language, payload.GetProperty("language").GetString());
        Assert.True(payload.GetProperty("referralCodePresent").GetBoolean());
        Assert.True(payload.GetProperty("termsAccepted").GetBoolean());
        Assert.Equal(_terms.Version, payload.GetProperty("termsVersion").GetString());
        Assert.Equal(_privacy.Version, payload.GetProperty("privacyVersion").GetString());

        var members = payload.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(6, members.Count);
        Assert.DoesNotContain(members, m => m.Contains("name", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, m => m.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, m => m.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Email, snapshot.AfterJson);
        Assert.DoesNotContain("FRIEND-1", snapshot.AfterJson);
        Assert.DoesNotContain("John", snapshot.AfterJson);
    }

    [Fact]
    public async Task A_Missing_Tick_Is_Recorded_As_Not_Asserted()
    {
        var result = await CreateHandler().Handle(Command(termsAccepted: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = Payload();
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("termsAccepted").ValueKind);
        Assert.False(payload.GetProperty("referralCodePresent").GetBoolean());
        Assert.Equal(_terms.Version, payload.GetProperty("termsVersion").GetString());
    }

    // No text seeded for the market is a configuration defect, not a reason to refuse an account: the
    // consent is still written, unversioned, and the evidence says "version unknown".
    [Fact]
    public async Task No_Document_In_Force_Still_Registers_Grants_Unversioned_And_Records_No_Version()
    {
        var handler = new Register.Handler(
            _cartRepository.Object, _userRepository.Object, new Mock<IReferralService>().Object, _pending,
            _consentService.Object, LegalDocumentFixtures.Resolver(null, null).Object, _auditContext,
            NullLogger<Register.Handler>.Instance);

        var result = await handler.Handle(Command(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _consentService.Verify(
            s => s.TryGrantAsync(_createdUser!.Id, ConsentType.TermsOfService, null, It.IsAny<CancellationToken>()),
            Times.Once);
        var payload = Payload();
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("termsVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("privacyVersion").ValueKind);
    }

    // The unconfirmed re-registration refreshes the code on the existing row; a tick on that second
    // attempt is still a grant (the service makes it a no-op when the row already holds it).
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
        _consentService.Verify(
            s => s.TryGrantAsync(existing.Id, ConsentType.TermsOfService, _terms, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(existing.Id, _auditContext.DrainSnapshot()!.ActorUserId);
    }
}
