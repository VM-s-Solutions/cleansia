using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// A cleaner's acceptances of their own documents are theirs to receive, row for row: which document and
/// text, the version, the instant, the client and the request context — in every operating company they
/// accepted under, and never another cleaner's.
/// </summary>
public sealed class CleanerLegalDocumentExportTests : IDisposable
{
    private const string CleanerUserId = "user-export-legal-cleaner";
    private const string CleanerEmployeeId = "employee-export-legal-1";
    private const string OtherEmployeeId = "employee-export-legal-2";

    private static readonly DateTimeOffset FrameworkAcceptedOn = new(2027, 1, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SelfBillingAcceptedOn = new(2027, 1, 5, 14, 30, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly LegalDocument _frameworkContract = Document(LegalDocumentType.CleanerFrameworkContract);
    private readonly LegalDocument _selfBillingAgreement = Document(LegalDocumentType.SelfBillingAgreement);

    public CleanerLegalDocumentExportTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task The_Cleaners_Export_Lists_Their_Acceptances_In_Every_Company_With_The_Request_Context_And_Nobody_Elses()
    {
        await SeedAsync();

        var export = await ExportAsync(CleanerUserId);

        Assert.Collection(
            export.CleanerLegalDocumentAcceptances,
            newest =>
            {
                Assert.Equal(LegalDocumentType.SelfBillingAgreement, newest.DocumentType);
                Assert.Equal(_selfBillingAgreement.TextFor("en")!.Id, newest.LegalDocumentTextId);
                Assert.Equal(_selfBillingAgreement.Version, newest.DocumentVersion);
                Assert.Equal("en", newest.Language);
                Assert.Equal(SelfBillingAcceptedOn, newest.AcceptedOn);
                Assert.Equal("cleansia.partner", newest.ClientAudience);
                Assert.Equal("198.51.100.4", newest.IpAddress);
                Assert.Equal("Firefox", newest.DeviceLabel);
                Assert.Null(newest.DeviceId);
            },
            oldest =>
            {
                Assert.Equal(LegalDocumentType.CleanerFrameworkContract, oldest.DocumentType);
                Assert.Equal(_frameworkContract.TextFor("cs")!.Id, oldest.LegalDocumentTextId);
                Assert.Equal(_frameworkContract.Version, oldest.DocumentVersion);
                Assert.Equal("cs", oldest.Language);
                Assert.Equal(FrameworkAcceptedOn, oldest.AcceptedOn);
                Assert.Equal("cleansia.mobile", oldest.ClientAudience);
                Assert.Equal("203.0.113.9", oldest.IpAddress);
                Assert.Equal("Pixel 8", oldest.DeviceLabel);
                Assert.Equal("device-export", oldest.DeviceId);
            });
    }

    private async Task<GdprExportDto> ExportAsync(string userId)
    {
        await using var ctx = NewContext(TestTenants.Default);
        var consents = new Mock<IUserConsentRepository>();
        consents.Setup(r => r.GetByUserIdNoTrackingAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var service = new GdprExportService(
            new UserRepository(ctx),
            new OrderRepository(ctx),
            new DisputeRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new EmployeeInvoiceRepository(ctx),
            new EmployeePayoutDetailsRepository(ctx),
            consents.Object,
            new CustomerActionAuditRepository(ctx),
            new WorkContractAcceptanceRepository(ctx),
            new LegalDocumentRepository(ctx),
            new CleanerLegalDocumentAcceptanceRepository(ctx));

        return await service.BuildAsync(userId, exportedBy: "self", CancellationToken.None);
    }

    private async Task SeedAsync()
    {
        await using (var schema = NewContext(TestTenants.Default))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using (var ctx = NewContext(TestTenants.Default))
        {
            var cleanerUser = User.CreateWithPassword("cleaner.legal@cleansia.test", "Test-password-1!", "Petra", "Svobodova", UserProfile.Employee);
            cleanerUser.Id = CleanerUserId;
            var cleaner = Employee.CreateWithUser(cleanerUser);
            cleaner.Id = CleanerEmployeeId;
            ctx.Add(cleaner);

            ctx.LegalDocuments.Add(_frameworkContract);
            ctx.LegalDocuments.Add(_selfBillingAgreement);

            ctx.Add(Acceptance(
                CleanerEmployeeId, _frameworkContract, "cs", FrameworkAcceptedOn,
                "cleansia.mobile", "203.0.113.9", "Pixel 8", "device-export"));
            ctx.Add(Acceptance(
                OtherEmployeeId, _frameworkContract, "en", FrameworkAcceptedOn,
                "cleansia.partner", "192.0.2.77", "Safari", "device-other"));

            await ctx.CommitAsync(CancellationToken.None);
        }

        await using (var secondCompany = NewContext(TestTenants.Second))
        {
            secondCompany.Add(Acceptance(
                CleanerEmployeeId, _selfBillingAgreement, "en", SelfBillingAcceptedOn,
                "cleansia.partner", "198.51.100.4", "Firefox", null));

            await secondCompany.CommitAsync(CancellationToken.None);
        }
    }

    private static LegalDocument Document(LegalDocumentType type)
    {
        var document = LegalDocument.Create(LegalDocumentAudience.Employee, type, null, new DateOnly(2027, 1, 1));
        document.AddText("en", "Cleaner document", "## Terms\n\nThe cleaner's terms.");
        document.AddText("cs", "Dokument pro pracovníka", "## Podmínky\n\nPodmínky pracovníka.");
        return document;
    }

    private static CleanerLegalDocumentAcceptance Acceptance(
        string employeeId,
        LegalDocument document,
        string language,
        DateTimeOffset acceptedOn,
        string clientAudience,
        string? ipAddress,
        string? deviceLabel,
        string? deviceId)
    {
        var acceptance = CleanerLegalDocumentAcceptance.Create(
            employeeId, document.TextFor(language)!, document.Version, clientAudience, ipAddress, deviceLabel, deviceId);
        typeof(CleanerLegalDocumentAcceptance)
            .GetProperty(nameof(CleanerLegalDocumentAcceptance.AcceptedOn))!
            .SetValue(acceptance, acceptedOn);
        return acceptance;
    }

    private CleansiaDbContext NewContext(string tenantId) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
