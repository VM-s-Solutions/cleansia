using System.Reflection;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Domain.Legal;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// Owner ruling 2026-09-28: the booking tick reappears when the customer's accepted terms or privacy
/// policy is not the text in force, and every client decides whether to show it from
/// <see cref="UserConsentDto.CoversCurrentVersion"/>. A read that answered "covered" for an older text
/// would hide the box from exactly the customers the booking then refuses.
/// </summary>
public sealed class ConsentCoversCurrentVersionTests
{
    private const string UserId = "user-consent-read";

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IUserConsentRepository> _consents = new();
    private readonly Mock<ILegalDocumentResolver> _resolver = new();

    private readonly LegalDocument _termsInForce = LegalDocumentFixtures.Terms(new DateOnly(2026, 9, 27));
    private readonly LegalDocument _olderTerms = LegalDocumentFixtures.Terms(new DateOnly(2026, 9, 14));
    private readonly LegalDocument _privacyInForce = LegalDocumentFixtures.Privacy(new DateOnly(2026, 9, 14));

    public ConsentCoversCurrentVersionTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _resolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_termsInForce);
        _resolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_privacyInForce);
    }

    [Fact]
    public async Task An_Acceptance_Of_An_Older_Text_Does_Not_Cover_The_One_In_Force()
    {
        OnRecord(Accepted(ConsentType.TermsOfService, _olderTerms));

        var terms = Assert.Single(await ReadAsync());

        Assert.Equal("2026-09-14", terms.DocumentVersion);
        Assert.False(terms.CoversCurrentVersion);
    }

    [Fact]
    public async Task An_Acceptance_Of_The_Text_In_Force_Covers_It()
    {
        OnRecord(Accepted(ConsentType.TermsOfService, _termsInForce), Accepted(ConsentType.PrivacyPolicy, _privacyInForce));

        var read = await ReadAsync();

        Assert.All(read, consent => Assert.True(consent.CoversCurrentVersion));
        Assert.Equal("2026-09-27", Assert.Single(read, c => c.ConsentType == ConsentType.TermsOfService).DocumentVersion);
    }

    [Fact]
    public async Task A_Withdrawn_Acceptance_Covers_Nothing()
    {
        OnRecord(Accepted(ConsentType.PrivacyPolicy, _privacyInForce).Withdraw());

        Assert.False(Assert.Single(await ReadAsync()).CoversCurrentVersion);
    }

    /// <summary>A cleaner's document is an employee-audience text; the customer text of the same type slot must not answer for it.</summary>
    [Fact]
    public async Task A_Cleaner_Document_Is_Judged_Against_The_Employee_Text_In_Force()
    {
        var contract = LegalDocument.Create(
            LegalDocumentAudience.Employee, LegalDocumentType.CleanerFrameworkContract, null, new DateOnly(2027, 1, 1));
        contract.AddText("en", "Framework contract", "## Terms");
        _resolver
            .Setup(r => r.ResolveInForceAsync(
                LegalDocumentAudience.Employee, LegalDocumentType.CleanerFrameworkContract, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);
        OnRecord(Accepted(ConsentType.CleanerFrameworkContract, contract));

        Assert.True(Assert.Single(await ReadAsync()).CoversCurrentVersion);
        _resolver.Verify(
            r => r.ResolveInForceAsync(LegalDocumentType.CleanerFrameworkContract, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void OnRecord(params UserConsent[] consents) =>
        _consents.Setup(r => r.GetByUserIdNoTrackingAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(consents.ToList());

    private static UserConsent Accepted(ConsentType type, LegalDocument document) =>
        UserConsent.Grant(UserId, type, "203.0.113.9", "Chrome", document.Version, document.Id);

    // The handler is internal and no project has InternalsVisibleTo, so it is built by reflection.
    private async Task<List<UserConsentDto>> ReadAsync()
    {
        var handler = (IQueryHandler<GetUserConsents.Query, List<UserConsentDto>>)Activator.CreateInstance(
            typeof(GetUserConsents).GetNestedType("Handler", BindingFlags.NonPublic)!,
            _session.Object, _consents.Object, _resolver.Object)!;

        var result = await handler.Handle(new GetUserConsents.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
