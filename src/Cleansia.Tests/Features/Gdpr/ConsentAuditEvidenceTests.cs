using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using Cleansia.Tests.Domain.Legal;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// ADR-0062 D3/D4 — the two consent commands are customer acts recorded by the pipeline. Asserted at
/// the producer: the marker is frozen, the handler passes the document in force ONLY for a customer
/// (the same command is routed on both Partner hosts, where an employee accepts a different document —
/// ADR-0041), resolved for the default market because a signed-in customer names none, and the
/// evidence carries the consent type and version, nothing about the person.
/// </summary>
public sealed class ConsentAuditEvidenceTests
{
    private const string UserId = "user-1";

    private readonly Mock<IConsentService> _consentService = new();
    private readonly Mock<IUserConsentRepository> _userConsentRepository = new();
    private readonly AuditContext _auditContext = new();
    private readonly LegalDocument _terms = LegalDocumentFixtures.Terms();
    private readonly Mock<ILegalDocumentResolver> _legalDocuments;

    public ConsentAuditEvidenceTests()
    {
        _legalDocuments = LegalDocumentFixtures.Resolver(_terms, LegalDocumentFixtures.Privacy());
    }

    private static IUserSessionProvider Session(UserProfile role) =>
        new TestUserSessionProvider(UserId, "user@cleansia.test", [new Claim(ClaimTypes.Role, role.ToString())]);

    private GrantConsent.Handler GrantHandler(UserProfile role) =>
        new(Session(role), _consentService.Object, _legalDocuments.Object, _auditContext);

    private WithdrawConsent.Handler WithdrawHandler(UserProfile role) =>
        new(Session(role), _userConsentRepository.Object, _auditContext);

    private static JsonElement Payload(AuditSnapshot? snapshot) =>
        JsonDocument.Parse(snapshot!.AfterJson!).RootElement;

    [Theory]
    [InlineData(typeof(GrantConsent.Command), "customer.consent.grant")]
    [InlineData(typeof(WithdrawConsent.Command), "customer.consent.withdraw")]
    public void The_Consent_Commands_Carry_A_Frozen_Customer_Marker_Keyed_On_The_User(Type commandType, string expectedLabel)
    {
        var descriptor = AuditActionDescriptor.For(commandType);

        Assert.Equal(expectedLabel, descriptor.Action);
        Assert.Equal("User", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.False(descriptor.AllowsAnonymousActor);
        Assert.False(descriptor.Sensitive);
    }

    [Fact]
    public async Task A_Customer_Grant_Passes_The_Document_In_Force_For_The_Default_Market_And_Records_Its_Version()
    {
        _consentService
            .Setup(s => s.TryGrantAsync(UserId, ConsentType.TermsOfService, _terms, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await GrantHandler(UserProfile.Customer)
            .Handle(new GrantConsent.Command(ConsentType.TermsOfService), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(UserId, snapshot.ResourceId);
        var payload = Payload(snapshot);
        Assert.Equal(_terms.Version, payload.GetProperty("documentVersion").GetString());
        Assert.Equal("termsOfService", payload.GetProperty("consentType").GetString());
        Assert.Equal(2, payload.EnumerateObject().Count());
        _legalDocuments.Verify(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Customer_Grant_Of_A_Consent_Without_A_Document_Passes_No_Version()
    {
        _consentService
            .Setup(s => s.TryGrantAsync(UserId, ConsentType.MarketingEmails, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await GrantHandler(UserProfile.Customer)
            .Handle(new GrantConsent.Command(ConsentType.MarketingEmails), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(JsonValueKind.Null, Payload(_auditContext.DrainSnapshot()).GetProperty("documentVersion").ValueKind);
        _legalDocuments.Verify(
            r => r.ResolveInForceAsync(It.IsAny<LegalDocumentType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task An_Employee_Grant_Passes_No_Version_Even_For_The_Terms()
    {
        _consentService
            .Setup(s => s.TryGrantAsync(UserId, ConsentType.TermsOfService, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await GrantHandler(UserProfile.Employee)
            .Handle(new GrantConsent.Command(ConsentType.TermsOfService), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _consentService.Verify(
            s => s.TryGrantAsync(UserId, ConsentType.TermsOfService, null, It.IsAny<CancellationToken>()), Times.Once);
        _consentService.Verify(
            s => s.TryGrantAsync(It.IsAny<string>(), It.IsAny<ConsentType>(), It.Is<LegalDocument?>(d => d != null), It.IsAny<CancellationToken>()),
            Times.Never);
        _legalDocuments.Verify(
            r => r.ResolveInForceAsync(It.IsAny<LegalDocumentType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task An_Already_Granted_Consent_Is_Refused_And_Records_No_Evidence()
    {
        _consentService
            .Setup(s => s.TryGrantAsync(UserId, ConsentType.TermsOfService, It.IsAny<LegalDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await GrantHandler(UserProfile.Customer)
            .Handle(new GrantConsent.Command(ConsentType.TermsOfService), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ConsentAlreadyGranted, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
    }

    [Fact]
    public async Task A_Withdrawal_Records_The_Version_The_Row_Was_Granted_Under()
    {
        var consent = UserConsent.Grant(UserId, ConsentType.PrivacyPolicy, "203.0.113.9", "Chrome", "2025-01-old");
        _userConsentRepository
            .Setup(r => r.GetByUserAndTypeAsync(UserId, ConsentType.PrivacyPolicy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consent);

        var result = await WithdrawHandler(UserProfile.Customer)
            .Handle(new WithdrawConsent.Command(ConsentType.PrivacyPolicy), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(consent.IsGranted);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal(UserId, snapshot!.ResourceId);
        Assert.Equal("2025-01-old", Payload(snapshot).GetProperty("documentVersion").GetString());
    }

    [Fact]
    public async Task A_Withdrawal_Of_A_Consent_Never_Given_Is_Refused_And_Records_No_Evidence()
    {
        var result = await WithdrawHandler(UserProfile.Customer)
            .Handle(new WithdrawConsent.Command(ConsentType.PrivacyPolicy), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ConsentNotFound, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
    }
}
