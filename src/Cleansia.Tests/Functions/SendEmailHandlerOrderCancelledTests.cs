using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
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
/// The cancellation arm of the send-email consumer, for a booking with an account. A no-cleaner cancellation
/// e-mails every booking, so a customer who uses only the web hears of it: the e-mail goes to the booking's
/// own address with the apology credit, carries the order page's link rather than a guest credential, and
/// reads the booking's language, then the account's. A guest keeps the language the producer chose.
/// </summary>
public sealed class SendEmailHandlerOrderCancelledTests : IDisposable
{
    private const string OrderId = "order-cancelled-account";
    private const string BookingEmail = "booking@cleansia.test";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly MutableTenantProvider _tenant = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly MemoryGuard _guard = new();

    public SendEmailHandlerOrderCancelledTests()
    {
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=OFF";
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task An_Accounts_Cancelled_Booking_Is_Emailed_With_The_Apology_And_No_Guest_Link()
    {
        await SeedAsync(OrderStatus.Cancelled, guest: false, bookedLanguage: "cs");
        await using var ctx = Context();
        var handler = Handler(ctx);

        await handler.HandleAsync(Body(apology: 250m), CancellationToken.None);
        await handler.HandleAsync(Body(apology: 250m), CancellationToken.None);

        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(BookingEmail, It.Is<Order>(o => o.Id == OrderId),
            "Cancelled", "cs", It.IsAny<CancellationToken>(), 1000m, null, 250m), Times.Once);
        Assert.Empty(await ctx.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
        Assert.True(await _guard.HasProcessedAsync(MessageKeys.GuestOrderCancelledEmail(OrderId)));
    }

    [Theory]
    [InlineData(null, "uk", "uk")]
    [InlineData("sk", "uk", "sk")]
    [InlineData(null, null, "ru")]
    public async Task An_Accounts_Email_Reads_The_Booking_Then_The_Account_Then_The_Message_Language(
        string? booked, string? account, string expected)
    {
        await SeedAsync(OrderStatus.Cancelled, guest: false, bookedLanguage: booked, accountLanguage: account);
        await using var ctx = Context();

        await Handler(ctx).HandleAsync(Body(apology: null, language: "ru"), CancellationToken.None);

        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(BookingEmail, It.IsAny<Order>(), "Cancelled", expected,
            It.IsAny<CancellationToken>(), 1000m, null, null), Times.Once);
    }

    [Fact]
    public async Task A_Guest_Keeps_The_Language_The_Producer_Chose()
    {
        await SeedAsync(OrderStatus.Cancelled, guest: true, bookedLanguage: "cs");
        await using var ctx = Context();

        await Handler(ctx).HandleAsync(Body(apology: null, language: "sk"), CancellationToken.None);

        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(BookingEmail, It.IsAny<Order>(), "Cancelled", "sk",
            It.IsAny<CancellationToken>(), 1000m, It.Is<string?>(token => !string.IsNullOrEmpty(token)), null), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_Renderer_Receives_The_No_Cleaner_Fact_From_The_Queued_Message(bool noCleanerAvailable)
    {
        await SeedAsync(OrderStatus.Cancelled, guest: false, bookedLanguage: "cs");
        await using var ctx = Context();

        await Handler(ctx).HandleAsync(Body(apology: null, noCleanerAvailable: noCleanerAvailable), CancellationToken.None);

        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(BookingEmail, It.IsAny<Order>(), "Cancelled", "cs",
            It.IsAny<CancellationToken>(), 1000m, null, null, noCleanerAvailable), Times.Once);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.Confirmed)]
    public async Task An_Accounts_Booking_That_Is_Not_Cancelled_Is_Not_Emailed(OrderStatus current)
    {
        await SeedAsync(current, guest: false, bookedLanguage: "cs");
        await using var ctx = Context();

        await Handler(ctx).HandleAsync(Body(apology: 250m), CancellationToken.None);

        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<decimal?>(), It.IsAny<string?>(),
            It.IsAny<decimal?>()), Times.Never);
    }

    private async Task SeedAsync(OrderStatus status, bool guest, string? bookedLanguage, string? accountLanguage = null)
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
            user = User.CreateWithPassword("account@cleansia.test", "Password123!", "Customer", "Cancelled", languageCode: accountLanguage);
            user.UpdateLanguagePreference(accountLanguage);
            ctx.Users.Add(user);
        }

        var order = Order.Create("Cancelled Customer", BookingEmail, "+420000000000",
            Address.Create("Test St 1", "Brno", "60200", "cz"), 1, 1, DateTime.UtcNow.AddHours(-1),
            PaymentType.Card, 1000m, currency.Id, PaymentStatus.Paid, userId: user?.Id,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetLanguage(bookedLanguage);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (status == OrderStatus.Cancelled)
        {
            order.Cancel(DateTime.UtcNow, CancelledBy.System, 0m, 1000m, OrderCancellationReasons.NoCleanerAvailable);
        }

        if (status != OrderStatus.New)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(status, order));
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

    private static string Body(decimal? apology, string language = "cs", bool noCleanerAvailable = false) => JsonSerializer.Serialize(
        new QueueEnvelope<SendGuestOrderCancellationEmailMessage>(
            MessageKeys.GuestOrderCancelledEmail(OrderId), TestTenants.Default,
            new SendGuestOrderCancellationEmailMessage(OrderId, language, 1000m, TestTenants.Default, apology, noCleanerAvailable)),
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
