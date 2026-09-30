using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Domain.Legal;

namespace Cleansia.Tests.Domain.Users;

/// <summary>
/// Owner ruling 2026-09-28: an acceptance stands for the text in force only when it is an acceptance of
/// that very text. A newer version is a different document, and so is a market's own copy seeded under
/// the same or a later date than the platform-wide one.
/// </summary>
public sealed class UserConsentCoversTests
{
    private static readonly DateOnly Earlier = new(2026, 9, 14);
    private static readonly DateOnly Later = new(2026, 9, 27);

    private static UserConsent Accepted(LegalDocument? document) =>
        UserConsent.Grant("user-1", ConsentType.TermsOfService, "203.0.113.9", "Chrome", document?.Version, document?.Id);

    [Fact]
    public void An_Acceptance_Of_The_Document_In_Force_Covers_It()
    {
        var inForce = LegalDocumentFixtures.Terms(Later);

        Assert.True(Accepted(inForce).Covers(inForce));
    }

    [Fact]
    public void An_Acceptance_Of_An_Older_Version_Does_Not_Cover_The_Newer_One()
    {
        Assert.False(Accepted(LegalDocumentFixtures.Terms(Earlier)).Covers(LegalDocumentFixtures.Terms(Later)));
    }

    [Fact]
    public void An_Acceptance_Of_The_Platform_Text_Does_Not_Cover_A_Market_Copy_Of_The_Same_Date()
    {
        var platformWide = LegalDocumentFixtures.Terms(Later);
        var marketCopy = LegalDocumentFixtures.Terms(Later, countryId: "country-svk");

        Assert.Equal(platformWide.Version, marketCopy.Version);
        Assert.False(Accepted(platformWide).Covers(marketCopy));
    }

    [Fact]
    public void An_Acceptance_Of_Another_Markets_Later_Text_Does_Not_Cover_The_One_In_Force()
    {
        var otherMarket = LegalDocumentFixtures.Terms(Later, countryId: "country-cze");
        var inForce = LegalDocumentFixtures.Terms(Earlier, countryId: "country-svk");

        Assert.False(Accepted(otherMarket).Covers(inForce));
    }

    [Fact]
    public void An_Unversioned_Acceptance_Covers_No_Text()
    {
        Assert.False(Accepted(null).Covers(LegalDocumentFixtures.Terms(Earlier)));
    }

    [Fact]
    public void Nothing_In_Force_Is_Covered_By_Any_Standing_Acceptance()
    {
        Assert.True(Accepted(null).Covers(null));
    }

    [Fact]
    public void A_Withdrawn_Acceptance_Covers_Nothing()
    {
        var inForce = LegalDocumentFixtures.Terms(Later);

        Assert.False(Accepted(inForce).Withdraw().Covers(inForce));
        Assert.False(Accepted(inForce).Withdraw().Covers(null));
    }
}
