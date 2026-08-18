using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// Which order statuses refuse an erasure — and the pin that keeps that answer equal to the platform's
/// other live-order set.
///
/// <para><c>OnTheWay</c> was absent from <c>GdprDeletionService</c>'s blocking set from the day it was
/// written, while the DEAD <c>Pending</c> was present. No ticket, ADR or comment records an intent to
/// exclude it, and no deliberate reading of "is this order still live" admits a dead status and refuses
/// one a cleaner is actively driving under. The cost was concrete: an erasure could complete while a
/// cleaner was en route to the subject's home, anonymizing the customer record underneath a job that
/// stays live and staffed.</para>
///
/// <para><b>Both directions are asserted.</b> Blocking everything satisfies "a live order stops an
/// erasure"; blocking nothing satisfies "a finished order does not". The theory drives all seven
/// statuses through the real service, so neither degenerate answer passes.</para>
///
/// <para>Real repositories over in-memory SQLite, mirroring <c>DeadLetterErasureTests</c> — the verdict
/// has to come from the service, not from a re-reading of its set.</para>
/// </summary>
public sealed class ErasureBlockingOrderStatusTests : IDisposable
{
    private const string SubjectUserId = "user-erase-status-1";
    private const string SubjectEmail = "zdenka.hruskova@cleansia.test";

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public ErasureBlockingOrderStatusTests()
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

    [Theory]
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.OnTheWay, true)]
    [InlineData(OrderStatus.InProgress, true)]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public async Task An_Erasure_Is_Refused_While_The_Subject_Still_Has_A_Live_Order(
        OrderStatus status, bool expectBlocked)
    {
        await SeedAsync(status);

        var result = await EraseAsync();

        Assert.Equal(expectBlocked, !result.IsSuccess);
        if (expectBlocked)
        {
            Assert.Equal(BusinessErrorMessage.GdprDeletionBlockedByOrder, result.Error!.Message);
        }
    }

    /// <summary>
    /// The two sets cannot be one artifact — <c>SlotBlockingStatuses</c> is a <c>static readonly</c>
    /// array because EF inlines it into SQL, and this one runs in memory over materialized rows — so
    /// they are pinned against each other instead. ADR-0037 D5 already treats the pair as the two
    /// conservative-direction readers of one question; this is that agreement made mechanical, so the
    /// next divergence fails rather than sits for a year.
    ///
    /// <para>Read by reflection because both are private, which is correct: neither is a shared
    /// platform grouping (ADR-0049 §D7) and promoting one to a shared name is how a set acquires
    /// callers whose question it never answered. A rename must redden this rather than silently stop
    /// comparing them.</para>
    /// </summary>
    [Fact]
    public void The_Erasure_Blocking_Set_And_The_Slot_Blocking_Set_Agree()
    {
        var erasure = PrivateStatusSet(typeof(GdprDeletionService), "ErasureBlockingStatuses");
        var slot = PrivateStatusSet(typeof(OrderRepository), "SlotBlockingStatuses");

        Assert.Equal(erasure, slot);
    }

    /// <summary>
    /// Stated positively as well, so a status added to <see cref="OrderStatus"/> and forgotten at both
    /// sites reddens here instead of silently defaulting to "not live". A new member is a decision, and
    /// this is where it gets made.
    /// </summary>
    [Fact]
    public void A_Live_Order_Is_Every_Status_Except_The_Two_TERMINAL_Ones()
    {
        var expected = Enum.GetValues<OrderStatus>()
            .Except([OrderStatus.Completed, OrderStatus.Cancelled])
            .Order()
            .ToList();

        Assert.Equal(expected, PrivateStatusSet(typeof(GdprDeletionService), "ErasureBlockingStatuses"));
        Assert.Equal(expected, PrivateStatusSet(typeof(OrderRepository), "SlotBlockingStatuses"));
    }

    private static List<OrderStatus> PrivateStatusSet(Type owner, string fieldName)
    {
        var field = owner.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.True(
            field is not null,
            $"{owner.Name}.{fieldName} no longer exists. This pin reads both live-order sets by name; "
            + "a rename must redden it rather than silently stop comparing them.");

        var value = Assert.IsAssignableFrom<IEnumerable<OrderStatus>>(field!.GetValue(null));
        return value.Distinct().Order().ToList();
    }

    private async Task<BusinessResult> EraseAsync()
    {
        await using var ctx = NewContext();
        var session = new TestUserSessionProvider(SubjectUserId, SubjectEmail);

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
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IStripeClient>(),
            _blobClientFactory.Object,
            NullLogger<GdprDeletionService>.Instance);

        return await service.DeleteUserAccountAsync(
            SubjectUserId, "gdpr_erasure_test", _ => ("test-actor", null), deferEmployeeErasure: false, CancellationToken.None);
    }

    private async Task SeedAsync(OrderStatus status)
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        var user = User.CreateWithPassword(
            SubjectEmail, "Test-password-1!", "Zdenka", "Hruskova", UserProfile.Customer);
        user.Id = SubjectUserId;
        ctx.Add(user);

        var order = Order.Create(
            customerName: "Zdenka Hruskova",
            customerEmail: SubjectEmail,
            customerPhone: "+420777222333",
            customerAddress: Address.Create("Erasure St 1", "Praha", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddHours(6),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: SubjectUserId);
        order.Id = "order-erase-status-1";
        order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        ctx.Add(order);

        await ctx.CommitAsync(CancellationToken.None);
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
