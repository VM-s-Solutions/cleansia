using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// The four residues <see cref="SubjectDataErasureRosterTests"/>'s model walk turned up in one pass, after
/// three sibling tables had been found by hand one at a time. Each is the same shape: the erasure had
/// already decided the artifact goes, and something adjacent to it stayed.
///
/// <list type="bullet">
/// <item><b>A logged-out <see cref="Device"/> tombstone.</b> Logout soft-deletes the row and leaves it
/// present so a later login can reclaim it — and BOTH paths that would remove it filtered on
/// <c>IsActive</c>: the erasure's own read, and the stale-device retention sweep. The handset id and push
/// token were reachable by neither.</item>
/// <item><b><see cref="EmployeeDocument"/> rows.</b> The erasure deletes every one of their blobs and left
/// the rows — file name (routinely the person's own), path, description, reviewer notes — pointing at bytes
/// that no longer exist. The superseded-document purge takes only already-deactivated rows, and an erasure
/// deactivates the employee, never their documents.</item>
/// <item><b><see cref="OrderPhoto"/> free text.</b> The order is retained and anonymized, and its own walk
/// blanks review, note and issue text; photos are not a navigation on the aggregate it loads, so the
/// uploader's file name and note survived next to a blob the erasure had already deleted.</item>
/// <item><b>Live <see cref="RefreshToken"/> rows.</b> Not a session hole — the refresh path already refuses
/// a deactivated user — but a retention one: the cleanup service hard-deletes only tokens that are revoked
/// OR expired, so an untouched live token keeps the subject's IP address and device label until its own
/// natural expiry before that clock even starts.</item>
/// </list>
///
/// <para>Every case asserts the residue is genuinely present before asserting its fate, and every case
/// asserts a bystander's equivalent row is untouched — deleting nothing satisfies the second direction and
/// deleting the table satisfies the first.</para>
/// </summary>
public sealed class SubjectResidueErasureTests : IDisposable
{
    private const string ErasedUserId = "user-erase-res-1";
    private const string ErasedEmployeeId = "employee-erase-res-1";
    private const string ErasedOrderId = "order-erase-res-1";
    private const string ErasedDeviceId = "device-erase-res-1";
    private const string ErasedPushToken = "fcm-token-erased-4d21";
    private const string ErasedDocumentName = "Novotna_Milada_ID.pdf";
    private const string ErasedPhotoOriginalName = "Milada_Novotna_kitchen.jpg";
    private const string ErasedPhotoNotes = "Left the key with Mrs Novotna's neighbour at no. 14.";

    private const string BystanderUserId = "user-keep-res-1";
    private const string BystanderEmployeeId = "employee-keep-res-1";
    private const string BystanderOrderId = "order-keep-res-1";
    private const string BystanderDeviceId = "device-keep-res-1";
    private const string BystanderDocumentName = "Svoboda_Tomas_ID.pdf";
    private const string BystanderPhotoOriginalName = "Tomas_Svoboda_hallway.jpg";

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();

    public SubjectResidueErasureTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(Mock.Of<IBlobContainerClient>());
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Erasure_Removes_A_Logged_Out_Device_Tombstone_As_Well_As_A_Live_One()
    {
        await SeedAsync();

        var seeded = await ReadAsync<Device>();
        Assert.Contains(seeded, d => d.UserId == ErasedUserId && !d.IsActive && d.DeviceToken == ErasedPushToken);

        await EraseAsync(ErasedUserId);

        var remaining = await ReadAsync<Device>();
        Assert.DoesNotContain(remaining, d => d.UserId == ErasedUserId);
        Assert.DoesNotContain(remaining, d => d.DeviceToken == ErasedPushToken);
        Assert.Contains(remaining, d => d.UserId == BystanderUserId && d.DeviceId == BystanderDeviceId);
    }

    [Fact]
    public async Task Erasure_Removes_The_Document_Rows_Whose_Blobs_It_Deleted()
    {
        await SeedAsync();

        var seeded = await ReadAsync<EmployeeDocument>();
        Assert.Contains(seeded, d => d.FileName == ErasedDocumentName);
        Assert.Contains(seeded, d => d.EmployeeId == ErasedEmployeeId && !d.IsActive);

        await EraseAsync(ErasedUserId);

        var remaining = await ReadAsync<EmployeeDocument>();
        Assert.DoesNotContain(remaining, d => d.EmployeeId == ErasedEmployeeId);
        Assert.DoesNotContain(remaining, d => d.FileName == ErasedDocumentName);
        Assert.Contains(remaining, d => d.FileName == BystanderDocumentName);
    }

    [Fact]
    public async Task Erasure_Blanks_The_Order_Photo_Free_Text_On_The_Row_It_Keeps()
    {
        await SeedAsync();

        var seeded = await ReadAsync<OrderPhoto>();
        Assert.Contains(seeded, p => p.OriginalFileName == ErasedPhotoOriginalName);
        Assert.Contains(seeded, p => p.Notes == ErasedPhotoNotes);

        await EraseAsync(ErasedUserId);

        var remaining = await ReadAsync<OrderPhoto>();
        var subjectPhoto = Assert.Single(remaining, p => p.OrderId == ErasedOrderId);
        Assert.Equal(AnonymizationMarker.Value, subjectPhoto.OriginalFileName);
        Assert.Null(subjectPhoto.Notes);

        var bystanderPhoto = Assert.Single(remaining, p => p.OrderId == BystanderOrderId);
        Assert.Equal(BystanderPhotoOriginalName, bystanderPhoto.OriginalFileName);
    }

    /// <summary>
    /// Asserted at the seam rather than over rows, because what matters is the exact call: the subject, no
    /// spared session, and a reason that is NOT <c>password_reset</c> — that string alone drives ADR-0027's
    /// revoked-user poll, and an erasure has no business firing it.
    /// </summary>
    [Fact]
    public async Task Erasure_Revokes_Every_Refresh_Token_The_Subject_Holds_Without_Firing_The_Reset_Directory()
    {
        await SeedAsync();

        await EraseAsync(ErasedUserId);

        _refreshTokenService.Verify(
            s => s.RevokeAllForUserAsync(
                ErasedUserId, GdprAuditReasons.RefreshTokenRevocation, null, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotEqual("password_reset", GdprAuditReasons.RefreshTokenRevocation);
    }

    private async Task EraseAsync(string userId)
    {
        await using var ctx = NewContext();
        var session = new TestUserSessionProvider(userId, $"{userId}@cleansia.test");
        var service = new GdprDeletionService(
            new UserRepository(ctx),
            new OrderRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new EmployeeInvoiceRepository(ctx),
            new EmployeePayoutDetailsRepository(ctx),
            new UserMembershipRepository(ctx),
            new OrderPhotoRepository(ctx),
            new DeviceRepository(ctx, session),
            new LiveActivityTokenRepository(ctx),
            new CartRepository(ctx),
            new UserConsentRepository(ctx),
            new GdprRequestRepository(ctx),
            new DisputeRepository(ctx),
            new SavedAddressRepository(ctx, session),
            new OrderEmployeePayRepository(ctx),
            new RecurringBookingTemplateRepository(ctx),
            new UserNotificationRepository(ctx),
            new DeadLetterRepository(ctx),
            new OutboxMessageRepository(ctx),
            _refreshTokenService.Object,
            Mock.Of<IStripeClient>(),
            _blobClientFactory.Object,
            NullLogger<GdprDeletionService>.Instance);

        var result = await service.DeleteUserAccountAsync(
            userId, "gdpr_erasure_test", _ => ("test-actor", null), deferEmployeeErasure: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<List<T>> ReadAsync<T>() where T : class
    {
        await using var ctx = NewContext();
        return await ctx.Set<T>().IgnoreQueryFilters().ToListAsync();
    }

    private async Task SeedAsync()
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        ctx.Add(NewUserWithEmployee(ErasedUserId, ErasedEmployeeId, "milada.novotna@cleansia.test", "Milada", "Novotna"));
        ctx.Add(NewUserWithEmployee(BystanderUserId, BystanderEmployeeId, "tomas.svoboda@cleansia.test", "Tomas", "Svoboda"));

        ctx.Add(NewDevice(ErasedUserId, $"{ErasedDeviceId}-live", "fcm-token-erased-live"));

        // The tombstone: logout deactivates the row and leaves it, and the stale-device sweep only takes
        // ACTIVE rows past their cutoff — so nothing but this walk ever reaches it.
        var tombstone = NewDevice(ErasedUserId, ErasedDeviceId, ErasedPushToken);
        tombstone.IsActive = false;
        ctx.Add(tombstone);

        ctx.Add(NewDevice(BystanderUserId, BystanderDeviceId, "fcm-token-kept-9a10"));

        ctx.Add(NewDocument(ErasedEmployeeId, ErasedDocumentName));
        var superseded = NewDocument(ErasedEmployeeId, $"v1_{ErasedDocumentName}");
        superseded.IsActive = false;
        ctx.Add(superseded);
        ctx.Add(NewDocument(BystanderEmployeeId, BystanderDocumentName));

        ctx.Add(NewOrder(ErasedUserId, ErasedOrderId));
        ctx.Add(NewOrder(BystanderUserId, BystanderOrderId));
        ctx.Add(NewPhoto(ErasedOrderId, ErasedPhotoOriginalName, ErasedPhotoNotes, ErasedEmployeeId));
        ctx.Add(NewPhoto(BystanderOrderId, BystanderPhotoOriginalName, "Nothing to report.", BystanderEmployeeId));

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static Employee NewUserWithEmployee(
        string userId, string employeeId, string email, string firstName, string lastName)
    {
        var user = User.CreateWithPassword(email, "Test-password-1!", firstName, lastName, UserProfile.Employee);
        user.Id = userId;

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;

        return employee;
    }

    private static Device NewDevice(string userId, string deviceId, string token) =>
        Device.Create(userId, "android", token, deviceId);

    private static EmployeeDocument NewDocument(string employeeId, string fileName) =>
        EmployeeDocument.Create(
            employeeId, fileName, $"documents/{employeeId}/{fileName}", "application/pdf", 2048,
            DocumentType.IdentityCard, description: "Identity document", createdBy: "seed");

    private static Order NewOrder(string userId, string orderId) =>
        OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = orderId,
            UserId = userId,
            CustomerAddress = Address.Create("Dlouha 14", "Praha", "11000", "CZ"),
            // Completed, because a New/Confirmed/InProgress order BLOCKS the erasure outright.
            CurrentStatus = OrderStatus.Completed,
        });

    private static OrderPhoto NewPhoto(string orderId, string originalFileName, string notes, string employeeId) =>
        OrderPhoto.Create(
            orderId, PhotoType.After, $"https://blobs.test/order-photos/{orderId}.jpg", $"{orderId}.jpg",
            originalFileName, 1024, "image/jpeg", employeeId, notes);

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
