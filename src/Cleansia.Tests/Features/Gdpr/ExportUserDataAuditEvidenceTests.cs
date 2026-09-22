using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// ADR-0062 D6 as amended by the owner's Q-AUD-O3 ruling — the customer's own export is a customer act
/// the pipeline records. Asserted at the producer: the marker is frozen on the user, and the evidence is
/// the section counts of what left the platform and nothing of what was in it — the export holds the
/// subject's whole record, and the audit row must never copy a byte of it.
/// </summary>
public sealed class ExportUserDataAuditEvidenceTests
{
    private const string UserId = "user-1";
    private const string Email = "jane.doe@example.test";
    private const string FirstName = "Jane";
    private const string LastName = "Doe";
    private const string Ip = "203.0.113.9";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IGdprExportService> _exports = new();
    private readonly Mock<IGdprRequestRepository> _requests = new();
    private readonly AuditContext _auditContext = new();

    private ExportUserData.Handler Handler() =>
        new(_users.Object, Session(), _exports.Object, _requests.Object, _auditContext);

    private static IUserSessionProvider Session() =>
        new TestUserSessionProvider(UserId, Email, [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);

    private static User Subject()
    {
        var user = User.CreateWithPassword(Email, "Password1!@abc", FirstName, LastName, UserProfile.Customer);
        user.Id = UserId;
        return user;
    }

    private static GdprExportDto ExportOf(string subjectId) =>
        new(
            new GdprExportProfileDto(subjectId, FirstName, LastName, Email, "+420123456789", null, "en", DateTimeOffset.UtcNow),
            Address: null,
            Employee: null,
            PayoutDetails: null,
            Orders:
            [
                new GdprExportOrderDto("order-1", "CZ-1", $"{FirstName} {LastName}", Email, OrderStatus.Completed, 1000m, DateTime.UtcNow, DateTimeOffset.UtcNow),
                new GdprExportOrderDto("order-2", "CZ-2", $"{FirstName} {LastName}", Email, OrderStatus.Cancelled, 800m, DateTime.UtcNow, DateTimeOffset.UtcNow),
            ],
            Disputes:
            [
                new GdprExportDisputeDto("dispute-1", "order-1", "CZ-1", "QualityIssue", "The kitchen floor was not mopped.", "Resolved",
                    "Partial refund issued.", 300m, "CZK", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    [new GdprExportDisputeMessageDto("Customer", DateTimeOffset.UtcNow, "Photos attached, the tiles are still grey.")],
                    ["kitchen-floor.jpg"]),
            ],
            Documents: [],
            Invoices: [],
            Consents:
            [
                new GdprExportConsentDto("consent-1", ConsentType.TermsOfService, true, DateTimeOffset.UtcNow, null, Ip, "Mozilla/5.0", "2026-09-14", "legal-doc-1"),
                new GdprExportConsentDto("consent-2", ConsentType.PrivacyPolicy, true, DateTimeOffset.UtcNow, null, Ip, "Mozilla/5.0", "2026-09-14", "legal-doc-2"),
                new GdprExportConsentDto("consent-3", ConsentType.MarketingEmails, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Ip, "Mozilla/5.0", null, null),
            ],
            CustomerActions:
            [
                new GdprExportCustomerActionDto("customer.order.cancel", DateTimeOffset.UtcNow, "Order", "order-2", true, null, "{\"feeRate\":0.5}", Ip, "iPhone 15"),
            ],
            new GdprExportMetadataDto(DateTimeOffset.UtcNow, Email, "JSON"), []);

    [Fact]
    public void The_Self_Export_Carries_A_Frozen_Customer_Marker_Keyed_On_The_User()
    {
        var descriptor = AuditActionDescriptor.For(typeof(ExportUserData.Command));

        Assert.Equal("customer.gdpr.export", descriptor.Action);
        Assert.Equal("User", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.False(descriptor.AllowsAnonymousActor);
        Assert.False(descriptor.Sensitive);
        Assert.True(descriptor.Audited);
    }

    [Fact]
    public void The_Evidence_Record_Is_Walked_By_The_Pii_Guard()
    {
        Assert.True(typeof(ICustomerAuditPayload).IsAssignableFrom(typeof(ExportUserData.GdprExportEvidence)));
    }

    [Fact]
    public async Task A_Successful_Export_Records_The_Section_Counts_On_The_Subject_And_Nothing_Of_The_Exported_Data()
    {
        _users.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Subject());
        _exports.Setup(s => s.BuildAsync(UserId, Email, It.IsAny<CancellationToken>())).ReturnsAsync(ExportOf(UserId));

        var result = await Handler().Handle(new ExportUserData.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(UserId, snapshot.ResourceId);
        Assert.Null(snapshot.BeforeJson);
        Assert.Null(snapshot.Reason);

        var payload = JsonDocument.Parse(snapshot.AfterJson!).RootElement;
        Assert.Equal(2, payload.GetProperty("orderCount").GetInt32());
        Assert.Equal(1, payload.GetProperty("disputeCount").GetInt32());
        Assert.Equal(3, payload.GetProperty("consentCount").GetInt32());
        Assert.Equal(1, payload.GetProperty("customerActionCount").GetInt32());
        Assert.Equal(0, payload.GetProperty("workContractAcceptanceCount").GetInt32());
        Assert.Equal(5, payload.EnumerateObject().Count());

        Assert.DoesNotContain(Email, snapshot.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(FirstName, snapshot.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(LastName, snapshot.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Ip, snapshot.AfterJson);
        Assert.DoesNotContain("feeRate", snapshot.AfterJson);
        Assert.DoesNotContain("2026-09-14", snapshot.AfterJson);
        Assert.DoesNotContain("legal-doc", snapshot.AfterJson);
        Assert.DoesNotContain("kitchen", snapshot.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", snapshot.AfterJson);
        Assert.DoesNotContain("+", snapshot.AfterJson);
    }

    [Fact]
    public async Task A_Successful_Export_Files_A_Completed_Request_Row_Processed_By_The_Fixed_Self_Actor_Never_The_Subjects_Address()
    {
        _users.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Subject());
        _exports.Setup(s => s.BuildAsync(UserId, Email, It.IsAny<CancellationToken>())).ReturnsAsync(ExportOf(UserId));
        GdprRequest? filed = null;
        _requests.Setup(r => r.Add(It.IsAny<GdprRequest>())).Callback<GdprRequest>(r => filed = r);

        var result = await Handler().Handle(new ExportUserData.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(filed);
        Assert.Equal(UserId, filed!.UserId);
        Assert.Equal(GdprAuditReasons.ExportRequestType, filed.RequestType);
        Assert.Equal(GdprRequestStatus.Completed, filed.Status);
        // The request row outlives the erasure, so the actor must never be the address it would keep.
        Assert.Equal(GdprAuditReasons.SelfActor, filed.ProcessedBy);
        Assert.DoesNotContain("@", filed.ProcessedBy);
    }

    [Fact]
    public async Task A_Session_Without_A_User_Row_Is_Refused_Builds_Nothing_And_Records_No_Evidence()
    {
        _users.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await Handler().Handle(new ExportUserData.Command(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.UserNotFound, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
        _exports.Verify(s => s.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _requests.Verify(r => r.Add(It.IsAny<GdprRequest>()), Times.Never);
    }
}
