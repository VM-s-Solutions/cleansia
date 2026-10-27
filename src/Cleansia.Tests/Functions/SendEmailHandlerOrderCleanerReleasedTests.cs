using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The "a cleaner can no longer come" arm of the send-email consumer. It is sent while the job has not
/// started and is still short of the cleaners it needs, once per release, in the booking's language, with a
/// committed link for a guest; a seat refilled since needs no notice, a booking cancelled or under way since
/// has no seat on offer, and an order with no live address has nobody to send to.
/// </summary>
public sealed class SendEmailHandlerOrderCleanerReleasedTests : IDisposable
{
    private const string OrderId = "order-cleaner-released";
    private const string AssignmentId = "assignment-released";
    private const string BookingEmail = "booking@cleansia.test";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly MutableTenantProvider _tenant = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly MemoryGuard _guard = new();

    public SendEmailHandlerOrderCleanerReleasedTests()
    {
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=OFF";
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task A_Customer_Whose_Order_Is_Back_At_New_Is_Emailed_Once_And_The_Release_Is_Claimed()
    {
        await SeedAsync(OrderStatus.New, guest: false, bookedLanguage: "cs");
        await using var ctx = Context();
        var handler = Handler(ctx);

        await handler.HandleAsync(Body(), CancellationToken.None);
        await handler.HandleAsync(Body(), CancellationToken.None);

        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(BookingEmail, It.Is<Order>(o => o.Id == OrderId),
            "CleanerReleased", "cs", It.IsAny<CancellationToken>(), null, null), Times.Once);
        Assert.True(await _guard.HasProcessedAsync(MessageKeys.OrderCleanerReleasedEmail(OrderId, AssignmentId)));
    }

    [Fact]
    public async Task A_Guest_Is_Emailed_A_Link_Whose_Token_Was_Committed_Before_The_Send()
    {
        await SeedAsync(OrderStatus.New, guest: true, bookedLanguage: "sk");
        _email.Setup(s => s.SendOrderStatusUpdateEmailAsync(BookingEmail, It.IsAny<Order>(), "CleanerReleased", "sk",
                It.IsAny<CancellationToken>(), null, It.IsAny<string?>()))
            .Returns(async (string _, Order _, string _, string _, CancellationToken _, decimal? _, string? rawToken, decimal? _) =>
            {
                Assert.NotNull(rawToken);
                await using var verify = Context();
                var token = Assert.Single(await verify.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(SecurityTokens.Hash(rawToken!), token.TokenHash);
                Assert.Null(token.RevokedOn);
                return "sent";
            });
        await using var ctx = Context();

        await Handler(ctx).HandleAsync(Body(), CancellationToken.None);

        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(BookingEmail, It.IsAny<Order>(), "CleanerReleased", "sk",
            It.IsAny<CancellationToken>(), null, It.IsAny<string?>()), Times.Once);
    }

    [Theory]
    [InlineData(null, "uk", "uk")]
    [InlineData(null, null, "ru")]
    [InlineData("de", "cs", "en")]
    public async Task The_Email_Uses_The_Booking_Then_The_Account_Then_The_Message_Language(
        string? booked, string? account, string expected)
    {
        await SeedAsync(OrderStatus.New, guest: false, bookedLanguage: booked, accountLanguage: account);
        await using var ctx = Context();

        await Handler(ctx).HandleAsync(Body(), CancellationToken.None);

        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(BookingEmail, It.IsAny<Order>(), "CleanerReleased", expected,
            It.IsAny<CancellationToken>(), null, null), Times.Once);
    }

    /// <summary>
    /// One of two cleaners left and the other is still on the job: the booking is Confirmed and short, so
    /// the customer is told.
    /// </summary>
    [Fact]
    public async Task A_Confirmed_Order_Still_Short_Of_Cleaners_Is_Emailed()
    {
        await SeedAsync(OrderStatus.Confirmed, guest: false, bookedLanguage: "cs", requiredCleaners: 2, crew: 1);
        await using var ctx = Context();

        await Handler(ctx).HandleAsync(Body(), CancellationToken.None);

        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(BookingEmail, It.Is<Order>(o => o.Id == OrderId),
            "CleanerReleased", "cs", It.IsAny<CancellationToken>(), null, null), Times.Once);
    }

    /// <summary>
    /// A seat refilled since the release leaves nothing to tell, a cancelled booking has no seat on offer, and
    /// a job under way has a crew at work, so the release's e-mail would now be wrong. Nothing is minted for a
    /// guest either.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.Confirmed, 1, 1)]
    [InlineData(OrderStatus.Confirmed, 2, 2)]
    [InlineData(OrderStatus.Cancelled, 1, 0)]
    [InlineData(OrderStatus.OnTheWay, 2, 1)]
    [InlineData(OrderStatus.InProgress, 2, 1)]
    public async Task An_Order_Back_At_Strength_Or_No_Longer_Before_Its_Start_Is_Not_Emailed(
        OrderStatus current, int requiredCleaners, int crew)
    {
        await SeedAsync(current, guest: true, bookedLanguage: "cs", requiredCleaners: requiredCleaners, crew: crew);
        await using var ctx = Context();

        await Handler(ctx).HandleAsync(Body(), CancellationToken.None);

        VerifyNothingSent();
        Assert.Empty(await ctx.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData(AnonymizationMarker.Value)]
    public async Task An_Order_Without_A_Live_Address_Is_Not_Emailed(string customerEmail)
    {
        await SeedAsync(OrderStatus.New, guest: true, bookedLanguage: "cs", customerEmail: customerEmail);
        await using var ctx = Context();

        await Handler(ctx).HandleAsync(Body(), CancellationToken.None);

        VerifyNothingSent();
        Assert.Empty(await ctx.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
    }

    private void VerifyNothingSent() =>
        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<decimal?>(), It.IsAny<string?>()), Times.Never);

    private async Task SeedAsync(
        OrderStatus status, bool guest, string? bookedLanguage, string? accountLanguage = null,
        string customerEmail = BookingEmail, int requiredCleaners = 1, int crew = 0)
    {
        await using var ctx = Context();
        await ctx.Database.EnsureCreatedAsync();
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = "czk";
        ctx.Currencies.Add(currency);
        var country = Country.Create("Czechia", "CZ", "CZ", true);
        country.Id = "cz";
        ctx.Countries.Add(country);
        User? user = null;
        if (!guest)
        {
            user = User.CreateWithPassword("account@cleansia.test", "Password123!", "Customer", "Released", languageCode: accountLanguage);
            user.UpdateLanguagePreference(accountLanguage);
            ctx.Users.Add(user);
        }
        var order = Order.Create("Released Customer", customerEmail, "+420000000000",
            Address.Create("Test St 1", "Brno", "60200", "cz"), 1, 1, DateTime.UtcNow.AddDays(2),
            PaymentType.Card, 1000m, currency.Id, PaymentStatus.Paid, userId: user?.Id,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetLanguage(bookedLanguage);
        order.UpdateEstimatedTime(requiredCleaners * 120).CalculateRequiredEmployees(spareSeats: 0);
        order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        for (var seat = 0; seat < crew; seat++)
        {
            var cleaner = Employee.CreateWithUser(
                User.CreateWithPassword($"cleaner-{seat}@cleansia.test", "Password123!", "Clean", "Er"));
            ctx.Employees.Add(cleaner);
            order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        }
        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private SendEmailHandler Handler(CleansiaDbContext ctx) => new(_email.Object, _guard, _tenant,
        Mock.Of<IPromoCodeRepository>(), Mock.Of<ITenantRepository>(), Mock.Of<ICompanyInfoRepository>(),
        NullLogger<SendEmailHandler>.Instance, new OrderRepository(ctx),
        new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)), ctx,
        Mock.Of<ICancellationPolicyResolver>(), Mock.Of<IReceivableRepository>(), Mock.Of<IContractConfirmationService>(),
        Mock.Of<IWorkContractAcceptanceRepository>(), Mock.Of<IEmployeeRepository>());

    private static string Body() => JsonSerializer.Serialize(
        new QueueEnvelope<SendOrderCleanerReleasedEmailMessage>(
            MessageKeys.OrderCleanerReleasedEmail(OrderId, AssignmentId), TestTenants.Default,
            new SendOrderCleanerReleasedEmailMessage(OrderId, AssignmentId, "ru", TestTenants.Default)),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private CleansiaDbContext Context() => new(new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"), _tenant);

    private sealed class MutableTenantProvider : ITenantProvider
    {
        private string? _value = TestTenants.Default;
        public string? GetCurrentTenantId() => _value;
        public void SetTenantOverride(string tenantId) => _value = tenantId;
        public void ClearTenantOverride() => _value = null;
    }

    private sealed class MemoryGuard : IIdempotencyGuard
    {
        private readonly HashSet<string> _keys = [];
        public Task<bool> AlreadyProcessedAsync(string key, CancellationToken ct = default) => Task.FromResult(!_keys.Add(key));
        public Task<bool> HasProcessedAsync(string key, CancellationToken ct = default) => Task.FromResult(_keys.Contains(key));
        public Task MarkProcessedAsync(string key, CancellationToken ct = default) { _keys.Add(key); return Task.CompletedTask; }
    }
}
