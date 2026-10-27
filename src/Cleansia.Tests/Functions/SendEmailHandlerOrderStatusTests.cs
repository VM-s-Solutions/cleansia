using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Common;
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

public sealed class SendEmailHandlerOrderStatusTests : IDisposable
{
    private const string OrderId = "order-status-consumer";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly MutableTenantProvider _tenant = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly MemoryGuard _guard = new();
    public SendEmailHandlerOrderStatusTests()
    {
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=OFF";
        command.ExecuteNonQuery();
    }
    public void Dispose() => _connection.Dispose();

    [Theory]
    [InlineData(OrderStatus.InProgress, "Started")]
    [InlineData(OrderStatus.Completed, "Completed")]
    public async Task A_Guest_Status_Email_Has_A_Committed_Live_Token_And_Is_Delivered_Once(OrderStatus status, string label)
    {
        await SeedAsync(status, guest: true, bookedLanguage: "cs");
        _email.Setup(s => s.SendOrderStatusUpdateEmailAsync("booking@cleansia.test", It.IsAny<Order>(), label, "cs",
            It.IsAny<CancellationToken>(), null, It.IsAny<string?>()))
            .Returns(async (string _, Order order, string _, string _, CancellationToken _, decimal? _, string? rawToken, decimal? _) =>
            {
                Assert.NotNull(rawToken);
                await using var verify = Context();
                var token = Assert.Single(await verify.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(SecurityTokens.Hash(rawToken!), token.TokenHash);
                Assert.Null(token.RevokedOn);
                Assert.True(token.ExpiresOn > DateTimeOffset.UtcNow);
                return "sent";
            });
        await using var ctx = Context();
        var handler = Handler(ctx);

        await handler.HandleAsync(Body(status), CancellationToken.None);
        await handler.HandleAsync(Body(status), CancellationToken.None);

        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync("booking@cleansia.test", It.IsAny<Order>(), label, "cs",
            It.IsAny<CancellationToken>(), null, It.IsAny<string?>()), Times.Once);
        Assert.True(await _guard.HasProcessedAsync(MessageKeys.OrderStatusEmail(OrderId, status)));
    }

    [Theory]
    [InlineData(null, "sk", "sk")]
    [InlineData(null, null, "ru")]
    [InlineData("de", "cs", "en")]
    public async Task A_Status_Email_Uses_Booking_Then_Account_Then_Message_Language(string? booked, string? account, string expected)
    {
        await SeedAsync(OrderStatus.Completed, guest: false, bookedLanguage: booked, accountLanguage: account);
        await using var ctx = Context();
        await Handler(ctx).HandleAsync(Body(OrderStatus.Completed), CancellationToken.None);
        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync("booking@cleansia.test", It.IsAny<Order>(), "Completed", expected,
            It.IsAny<CancellationToken>(), null, null), Times.Once);
    }

    [Theory]
    [InlineData(OrderStatus.InProgress, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.InProgress, OrderStatus.Completed)]
    [InlineData(OrderStatus.Completed, OrderStatus.InProgress)]
    public async Task A_Stale_Status_Message_Sends_And_Stages_Nothing(OrderStatus message, OrderStatus current)
    {
        await SeedAsync(current, guest: true, bookedLanguage: "cs");
        await using var ctx = Context();
        await Handler(ctx).HandleAsync(Body(message), CancellationToken.None);
        _email.Verify(s => s.SendOrderStatusUpdateEmailAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<decimal?>(), It.IsAny<string?>()), Times.Never);
        Assert.Empty(await ctx.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
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
            user = User.CreateWithPassword("account@cleansia.test", "Password123!", "Customer", "Status", languageCode: accountLanguage);
            user.UpdateLanguagePreference(accountLanguage);
            ctx.Users.Add(user);
        }
        var order = Order.Create("Status Customer", "booking@cleansia.test", "+420000000000",
            Address.Create("Test St 1", "Brno", "60200", "cz"), 1, 1, DateTime.UtcNow.AddHours(3),
            PaymentType.Card, 1000m, currency.Id, PaymentStatus.Paid, userId: user?.Id,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetLanguage(bookedLanguage);
        order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private SendEmailHandler Handler(CleansiaDbContext ctx) => new(_email.Object, _guard, _tenant,
        Mock.Of<IPromoCodeRepository>(), Mock.Of<ITenantRepository>(), Mock.Of<ICompanyInfoRepository>(),
        NullLogger<SendEmailHandler>.Instance, new OrderRepository(ctx),
        new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)), ctx,
        Mock.Of<ICancellationPolicyResolver>(), Mock.Of<IReceivableRepository>(), Mock.Of<IContractConfirmationService>(),
        Mock.Of<IWorkContractAcceptanceRepository>(), Mock.Of<IEmployeeRepository>());
    private static string Body(OrderStatus status) => JsonSerializer.Serialize(new QueueEnvelope<SendOrderStatusEmailMessage>(
        MessageKeys.OrderStatusEmail(OrderId, status), TestTenants.Default,
        new SendOrderStatusEmailMessage(OrderId, status, "ru", TestTenants.Default)),
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
