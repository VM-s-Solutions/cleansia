using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// T-0587 — a <see cref="LiveActivityToken"/> is the same fact as the <see cref="Device"/> row the erasure
/// already deletes: this subject's handset, and a live APNs address for it. Erasure took one and left the
/// other.
///
/// <para><b>Why the asymmetry existed.</b> ADR-0029 D3 split the activity registration OFF the
/// <c>Device</c> aggregate deliberately — update tokens are per (device × order), rotate mid-activity and
/// outlive no <c>Device</c> row — and the erasure walks named repositories one at a time with no navigation
/// from <c>User</c> to reach through. So the older table was in the walk and its new sibling was never
/// added. The entity's own doc enumerates its four deletion paths (terminal-send cleanup, the APNs 410
/// prune, the 24h janitor, the logout/revoke cascade) and erasure is not among them.</para>
///
/// <para><b>Why it is erased rather than retained.</b> The row is keyed on <c>UserId</c>, so it is not
/// pseudonymous in the sense that would excuse it — after the erasure that id resolves to an anonymized
/// user and the row's only remaining meaning is "this deleted person's handset can still be pushed to". Its
/// recovery value is zero: the activity it addresses belongs to an order that has just been anonymized on
/// an account that has just been deactivated. And retention here is unbounded BY CONSTRUCTION — the janitor
/// reclaims order-scoped rows only (<c>OrderId != null</c>), and the logout/revoke cascade needs a session
/// the erasure has just ended, so the per-install push-to-start row (<c>OrderId == null</c>) has no reaper
/// at all. <see cref="Erasure_Removes_The_Push_To_Start_Row_Nothing_Else_Ever_Reclaims"/> is that half.</para>
///
/// <para>Real repositories over in-memory SQLite, mirroring <c>DeadLetterErasureTests</c>; only the external
/// edges (blobs, Stripe) are doubles.</para>
/// </summary>
public sealed class LiveActivityTokenErasureTests : IDisposable
{
    private const string ErasedUserId = "user-erase-la-1";
    private const string ErasedEmail = "milada.novotna@cleansia.test";
    private const string ErasedDeviceId = "device-erase-la-1";
    private const string ErasedUpdateToken = "apns-update-token-erased-9f2c41ae";
    private const string ErasedPushToStartToken = "apns-pts-token-erased-3b71dd04";

    private const string BystanderUserId = "user-keep-la-1";
    private const string BystanderEmail = "tomas.svoboda@cleansia.test";
    private const string BystanderDeviceId = "device-keep-la-1";
    private const string BystanderToken = "apns-update-token-kept-77c0";

    private const string RegistrationTenantId = "tenant-alpha";

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public LiveActivityTokenErasureTests()
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
    public async Task Erasure_Removes_The_Subjects_Device_Identity_And_Apns_Address()
    {
        await SeedAsync();

        var seeded = await ReadRowsAsync();
        Assert.Contains(seeded, t => t.Token == ErasedUpdateToken);
        Assert.Contains(seeded, t => t.DeviceId == ErasedDeviceId);

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.DoesNotContain(remaining, t => t.UserId == ErasedUserId);
        Assert.DoesNotContain(remaining, t => t.DeviceId == ErasedDeviceId);
        Assert.DoesNotContain(remaining, t => t.Token == ErasedUpdateToken);
    }

    /// <summary>
    /// The per-install push-to-start row (<c>OrderId == null</c>). The 24h janitor takes order-scoped rows
    /// only, and the logout/revoke cascade is keyed on a session the erasure has just ended — so nothing
    /// else ever reclaims this one. It is asserted separately from the order-scoped row above precisely
    /// because a walk narrowed to "this user's orders" would pass that one and leave this one forever.
    /// </summary>
    [Fact]
    public async Task Erasure_Removes_The_Push_To_Start_Row_Nothing_Else_Ever_Reclaims()
    {
        await SeedAsync();

        var seeded = await ReadRowsAsync();
        Assert.Contains(seeded, t => t.OrderId == null && t.Token == ErasedPushToStartToken);

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.DoesNotContain(remaining, t => t.Token == ErasedPushToStartToken);
    }

    [Fact]
    public async Task Erasure_Leaves_Another_Subjects_Registrations_Untouched()
    {
        await SeedAsync();

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        var kept = Assert.Single(remaining);
        Assert.Equal(BystanderUserId, kept.UserId);
        Assert.Equal(BystanderDeviceId, kept.DeviceId);
        Assert.Equal(BystanderToken, kept.Token);
    }

    private async Task EraseAsync(string userId)
    {
        await using var ctx = NewContext();
        var session = new TestUserSessionProvider(userId, $"{userId}@cleansia.test");
        var service = new GdprDeletionService(
            new UserRepository(ctx),
            new OrderRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new DocumentDeletionRequestRepository(ctx),
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
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IStripeClient>(),
            _blobClientFactory.Object,
            NullLogger<GdprDeletionService>.Instance);

        var result = await service.DeleteUserAccountAsync(
            userId, "gdpr_erasure_test", _ => ("test-actor", null), deferEmployeeErasure: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<List<LiveActivityToken>> ReadRowsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<LiveActivityToken>().IgnoreQueryFilters().ToListAsync();
    }

    private async Task SeedAsync()
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        ctx.Add(NewUser(ErasedUserId, ErasedEmail, "Milada", "Novotna"));
        ctx.Add(NewUser(BystanderUserId, BystanderEmail, "Tomas", "Svoboda"));

        // Stamped with a real tenant the erasing request does not carry: registration is an authenticated
        // mobile route, so the row takes the SUBJECT's tenant, while an admin-driven erasure arrives with
        // the actor's — or with none. A tenant-scoped read cannot see this row.
        ctx.Add(LiveActivityToken.Create(
            ErasedUserId, ErasedDeviceId, "order-la-1", ErasedUpdateToken, RegistrationTenantId));
        ctx.Add(LiveActivityToken.Create(
            ErasedUserId, ErasedDeviceId, null, ErasedPushToStartToken, RegistrationTenantId));

        ctx.Add(LiveActivityToken.Create(
            BystanderUserId, BystanderDeviceId, "order-la-2", BystanderToken, tenantId: null));

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static User NewUser(string userId, string email, string firstName, string lastName)
    {
        var user = User.CreateWithPassword(email, "Test-password-1!", firstName, lastName, UserProfile.Customer);
        user.Id = userId;
        return user;
    }

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
