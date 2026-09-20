using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// The handler hands the assembled data to the renderer, names the file after the subject and the day,
/// and records an admin snapshot of ids, counts and the file's hash — never the content the file was
/// built to print (ADR-0012 D4.1).
/// </summary>
public sealed class ExportCustomerIncidentFileHandlerTests
{
    private const string SubjectId = "subject-1";
    private const string AdminEmail = "admin@cleansia.test";
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset GeneratedAt = new(2026, 9, 14, 23, 30, 0, TimeSpan.FromHours(2));

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IIncidentFileService> _incidentFileService = new();
    private readonly Mock<IPdfService> _pdfService = new();
    private readonly AuditContext _auditContext = new();

    public ExportCustomerIncidentFileHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns("admin-1");
        _session.Setup(s => s.GetUserEmail()).Returns(AdminEmail);
        _pdfService.Setup(p => p.GenerateIncidentFilePdf(It.IsAny<IncidentFilePdfData>()))
            .Returns(new IncidentFilePdf([0x25, 0x50, 0x44, 0x46], Sha));
    }

    [Fact]
    public async Task Builds_For_The_Admin_Renders_And_Names_The_File_After_The_Subject_And_The_Utc_Day()
    {
        _incidentFileService.Setup(s => s.BuildAsync(SubjectId, "order-1", AdminEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data(orderIdFilter: "order-1"));

        var result = await Handler().Handle(new ExportCustomerIncidentFile.Command(SubjectId, "order-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([0x25, 0x50, 0x44, 0x46], result.Value.PdfBytes);
        // 23:30 at +02:00 is 21:30 UTC the same day; the name follows the UTC clock the footer prints.
        Assert.Equal("incident-subject-1-20260914.pdf", result.Value.FileName);
    }

    [Fact]
    public async Task A_Blank_Order_Scope_Is_No_Scope()
    {
        _incidentFileService.Setup(s => s.BuildAsync(SubjectId, null, AdminEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data(orderIdFilter: null));

        var result = await Handler().Handle(new ExportCustomerIncidentFile.Command(SubjectId, "  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _incidentFileService.Verify(s => s.BuildAsync(SubjectId, null, AdminEmail, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_Snapshot_Carries_Ids_Counts_And_The_Hash_And_Nothing_The_File_Prints()
    {
        _incidentFileService.Setup(s => s.BuildAsync(SubjectId, "order-1", AdminEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data(orderIdFilter: "order-1"));

        await Handler().Handle(new ExportCustomerIncidentFile.Command(SubjectId, "order-1"), CancellationToken.None);

        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(SubjectId, snapshot.ResourceId);
        Assert.Equal(snapshot.BeforeJson, snapshot.AfterJson);

        var after = JsonDocument.Parse(snapshot.AfterJson!).RootElement;
        Assert.Equal(SubjectId, after.GetProperty("subjectUserId").GetString());
        Assert.Equal("order-1", after.GetProperty("orderId").GetString());
        Assert.Equal(1, after.GetProperty("orderCount").GetInt32());
        Assert.Equal(1, after.GetProperty("disputeCount").GetInt32());
        Assert.Equal(1, after.GetProperty("consentCount").GetInt32());
        Assert.Equal(2, after.GetProperty("trailEntryCount").GetInt32());
        Assert.Equal(Sha, after.GetProperty("dataSha256").GetString());
        Assert.Equal(7, after.EnumerateObject().Count());

        Assert.DoesNotContain("jane", snapshot.AfterJson!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", snapshot.AfterJson);
        Assert.DoesNotContain("ORD-", snapshot.AfterJson);
        Assert.DoesNotContain("203.0.113.9", snapshot.AfterJson);
        Assert.DoesNotContain("Kitchen", snapshot.AfterJson);
    }

    private ExportCustomerIncidentFile.Handler Handler() =>
        new(_session.Object, _incidentFileService.Object, _pdfService.Object, _auditContext);

    private static IncidentFilePdfData Data(string? orderIdFilter) =>
        new(
            new IncidentFileSubject(SubjectId, "Jane", "Doe", "jane@example.test", "+420123456789", GeneratedAt.AddYears(-1), "Cleansia CZ s.r.o.", "Czechia (CZ)", "en", false, null),
            orderIdFilter,
            [
                new IncidentFileOrder("order-1", "ORD-1", GeneratedAt.AddDays(-9), GeneratedAt.AddDays(-7).UtcDateTime, null, null, "Order St 9, Brno",
                    [], 1500m, "CZK", "Cash", "Pending", "Confirmed", [], [], [], null, null),
            ],
            [],
            [new IncidentFileDispute("disp-1", "ORD-1", "ServiceQuality", "Pending", "Kitchen untouched.", GeneratedAt.AddDays(-6), [], [], null, null, null, null)],
            [new IncidentFileConsent("TermsOfService", "2026-09-14", new DateOnly(2026, 9, 14), true, GeneratedAt.AddYears(-1), null, "203.0.113.9", "UA")],
            [
                new IncidentFileTrailEntry("Customer", GeneratedAt.AddDays(-8), "Customer", SubjectId, "customer.order.cancel", "Order", "order-1", true, null, [], "203.0.113.9", "iPhone"),
                new IncidentFileTrailEntry("Admin", GeneratedAt.AddDays(-5), "Administrator", "admin-1", "order.refund.full", "Order", "order-1", true, null, [], null, null),
            ],
            TrailTruncated: false,
            GeneratedAt,
            AdminEmail);
}
