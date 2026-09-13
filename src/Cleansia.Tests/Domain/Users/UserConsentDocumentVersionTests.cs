using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Users;

/// <summary>
/// ADR-0062 D4 — a consent row records the version of the document in force when it was granted, and a
/// re-acceptance under a different version moves the row (the audit trail keeps the history; the row is
/// the truth about now).
/// </summary>
public sealed class UserConsentDocumentVersionTests
{
    private const string UserId = "user-1";

    [Fact]
    public void Grant_Stamps_The_Document_Version()
    {
        var consent = UserConsent.Grant(UserId, ConsentType.TermsOfService, "203.0.113.9", "Chrome", "2026-09-draft");

        Assert.Equal("2026-09-draft", consent.DocumentVersion);
        Assert.True(consent.IsGranted);
    }

    [Fact]
    public void Grant_Without_A_Version_Leaves_It_Null()
    {
        var consent = UserConsent.Grant(UserId, ConsentType.DataProcessing, "203.0.113.9", "Chrome", null);

        Assert.Null(consent.DocumentVersion);
    }

    [Fact]
    public void Regrant_Replaces_The_Version_Along_With_The_Request_Context()
    {
        var consent = UserConsent
            .Grant(UserId, ConsentType.TermsOfService, "198.51.100.1", "Firefox", "2025-01-old")
            .Withdraw();

        consent.Regrant("203.0.113.9", "Chrome", "2026-09-draft");

        Assert.True(consent.IsGranted);
        Assert.Null(consent.WithdrawnAt);
        Assert.Equal("2026-09-draft", consent.DocumentVersion);
        Assert.Equal("203.0.113.9", consent.IpAddress);
        Assert.Equal("Chrome", consent.UserAgent);
    }

    [Fact]
    public void AcceptVersion_Moves_A_Granted_Row_To_A_Different_Version_And_Restamps_When_And_Where()
    {
        var consent = UserConsent.Grant(UserId, ConsentType.TermsOfService, "198.51.100.1", "Firefox", "2025-01-old");
        var grantedBefore = consent.GrantedAt;

        consent.AcceptVersion("2026-09-draft", "203.0.113.9", "Chrome");

        Assert.True(consent.IsGranted);
        Assert.Null(consent.WithdrawnAt);
        Assert.Equal("2026-09-draft", consent.DocumentVersion);
        Assert.Equal("203.0.113.9", consent.IpAddress);
        Assert.Equal("Chrome", consent.UserAgent);
        Assert.NotNull(consent.GrantedAt);
        Assert.True(consent.GrantedAt >= grantedBefore);
    }
}
