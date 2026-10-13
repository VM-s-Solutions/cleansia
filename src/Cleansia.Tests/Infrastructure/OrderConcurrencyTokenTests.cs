using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// An order moves on two lifecycle axes, fulfilment (<c>CurrentStatus</c>) and payment (<c>PaymentStatus</c>), and
/// both are its concurrency tokens: a writer that decided on the order as it read it is refused at commit when
/// another writer has moved either axis since. Before the payment axis was a token, a cancel decided on an unpaid
/// order that was paid in the meantime landed, and the order ended cancelled with the money kept and nothing
/// refunded. A token only guards a write that goes through the change tracker, so the walk at the end pins that
/// nothing in production writes <c>Orders</c> any other way. → /decisions/adr-0071
/// </summary>
public sealed class OrderConcurrencyTokenTests : IDisposable
{
    private const string OrderId = "order-token";

    private readonly SqliteConnection _connection;

    public OrderConcurrencyTokenTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // The order is seeded without its currency and country graph; this suite is about the order row.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void The_Orders_Concurrency_Tokens_Are_Its_Two_Lifecycle_Axes()
    {
        // Npgsql, the provider production runs: a row-version token such as xmin would only show up there.
        // No connection is opened; EF builds the model lazily.
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql("Host=model-only;Database=model-only")
            .Options;
        using var ctx = new CleansiaDbContext(
            options, new TestUserSessionProvider("system", "system@cleansia.test"), new FixedTenantProvider());

        var tokens = ctx.Model.FindEntityType(typeof(Order))!.GetProperties()
            .Where(p => p.IsConcurrencyToken)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal([nameof(Order.CurrentStatus), nameof(Order.PaymentStatus)], tokens);
    }

    [Fact]
    public async Task A_Cancel_Decided_On_An_Unpaid_Order_Is_Refused_Once_Its_Payment_Has_Landed()
    {
        await SeedUnpaidCardOrderAsync();

        await using var cancelling = NewContext();
        var read = await cancelling.Orders.Include(o => o.OrderStatusHistory).SingleAsync(o => o.Id == OrderId);

        await using (var paying = NewContext())
        {
            var order = await paying.Orders.SingleAsync(o => o.Id == OrderId);
            order.UpdatePaymentStatus(PaymentStatus.Paid);
            await paying.CommitAsync(CancellationToken.None);
        }

        read.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, read));
        read.Cancel(DateTime.UtcNow, CancelledBy.Customer, feeRate: 0m, refundAmount: 0m, reason: null);
        var refused = await Record.ExceptionAsync(() => cancelling.CommitAsync(CancellationToken.None));

        await using var verify = NewContext();
        var stored = await verify.Orders.AsNoTracking().SingleAsync(o => o.Id == OrderId);
        Assert.Equal(
            (PaymentStatus.Paid, OrderStatus.New, (DateTime?)null),
            (stored.PaymentStatus, stored.CurrentStatus, stored.CancelledAt));
        Assert.False(await verify.Set<OrderStatusTrack>().AnyAsync(t => t.Status == OrderStatus.Cancelled));
        Assert.IsType<DbUpdateConcurrencyException>(refused);
    }

    /// <summary>
    /// The tokens are compared only on an UPDATE the change tracker issues. A raw <c>UPDATE</c> or
    /// <c>DELETE</c> of <c>Orders</c>, or an <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> chained from the orders
    /// set, writes the row without comparing either axis, and commits outside the caller's unit of work. Scope:
    /// production sources (no test project, no migration); a full-line comment is not read; an
    /// <c>ExecuteUpdate</c> on a query held in a variable from an earlier statement is not seen.
    /// </summary>
    [Fact]
    public void Orders_Are_Written_Only_Through_The_Change_Tracker()
    {
        var corpus = ProductionSources().ToList();

        Assert.NotEmpty(corpus);
        Assert.Contains(corpus, f => f.Path.EndsWith(Path.Combine("Repositories", "OrderRepository.cs"), StringComparison.Ordinal));
        var audit = Assert.Single(corpus,
            f => f.Path.EndsWith(Path.Combine("Repositories", "EmployeeActionAuditRepository.cs"), StringComparison.Ordinal));
        Assert.Contains("FROM \"Orders\"", audit.Text, StringComparison.Ordinal);

        var offenders = corpus
            .SelectMany(f => Offences(Path.GetFileName(f.Path), f.Text)
                .Select(statement => $"{Path.GetRelativePath(SourceRoot(), f.Path)}: {statement}"))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Orders is written outside the change tracker. Such a write compares neither of the order's tokens "
            + "(CurrentStatus, PaymentStatus) and commits on its own, so it can overwrite a payment or a status "
            + "another writer committed. Load the order tracked and let the unit of work commit it "
            + "(→ /decisions/adr-0071). Offenders:\n  " + string.Join("\n  ", offenders));
    }

    [Theory]
    [InlineData("""await context.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, PaymentStatus.Paid), ct);""")]
    [InlineData("""await orderRepository.GetQueryableIgnoringTenant().Where(o => o.Id == id).ExecuteDeleteAsync(ct);""")]
    [InlineData("""context.Set<Order>().Where(o => o.IsActive).ExecuteDelete();""")]
    [InlineData("""await context.Database.ExecuteSqlRawAsync("UPDATE \"Orders\" SET \"PaymentStatus\" = 2 WHERE \"Id\" = {0}", id);""")]
    [InlineData("""await connection.ExecuteAsync(@"DELETE FROM ""Orders"" WHERE ""Id"" = @id");""")]
    [InlineData(""""var sql = """UPDATE "Orders" SET "CurrentStatus" = 5 WHERE "Id" = @id""";"""")]
    public void A_Write_Of_Orders_Outside_The_Tracker_Is_Caught(string statement)
    {
        Assert.NotEmpty(Offences("Sample.cs", statement));
    }

    [Theory]
    [InlineData(""""await ownContext.Database.ExecuteSqlAsync($"""SELECT "Id" FROM "Orders" WHERE "Id" = {id} FOR UPDATE""", ct);"""")]
    [InlineData("""await guestOrderAccessTokenRepository.GetQueryable().Where(t => t.RevokedOn != null).ExecuteDeleteAsync(ct);""")]
    [InlineData("""await context.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.AdminRole, role), ct);""")]
    [InlineData("""await context.Database.ExecuteSqlRawAsync("UPDATE \"OrderStatusTracks\" SET \"TenantId\" = {0}", tenantId);""")]
    [InlineData("""    // Not context.Orders.Where(o => o.Id == id).ExecuteDeleteAsync(ct): it would commit on its own.""")]
    [InlineData("""/// <remarks>Never UPDATE "Orders" by hand.</remarks>""")]
    public void A_Read_Or_A_Write_Of_Another_Table_Is_Not_Caught(string statement)
    {
        Assert.Empty(Offences("Sample.cs", statement));
    }

    private static readonly Regex RawOrdersWrite = new(
        """\b(?:UPDATE|DELETE\s+FROM)\s+(?:ONLY\s+)?(?:[\\"]*public[\\"]*\.)?[\\"]*Orders[\\"]*(?!\w)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BulkCall = new(
        @"\.Execute(?:Update|Delete)(?:Async)?\s*\(", RegexOptions.CultureInvariant);

    private static readonly Regex OrdersSource = new(
        @"\.Orders\b|\bSet<Order>\s*\(|(?i:\b_?I?OrderRepository\b)", RegexOptions.CultureInvariant);

    /// <summary>
    /// The offending statements of one source file. A bulk call's statement runs back to the nearest
    /// <c>;</c>, <c>{</c> or <c>}</c>; inside <c>OrderRepository.cs</c> every bulk call is on <c>Orders</c>.
    /// </summary>
    private static IEnumerable<string> Offences(string fileName, string source)
    {
        var code = string.Join('\n', source.Split('\n')
            .Select(line => line.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : line));

        foreach (Match raw in RawOrdersWrite.Matches(code))
        {
            yield return raw.Value;
        }

        foreach (Match call in BulkCall.Matches(code))
        {
            var start = code.LastIndexOfAny([';', '{', '}'], call.Index) + 1;
            var statement = code[start..call.Index];
            if (fileName == "OrderRepository.cs" || OrdersSource.IsMatch(statement))
            {
                yield return (statement.Trim() + call.Value).ReplaceLineEndings(" ");
            }
        }
    }

    private static IEnumerable<(string Path, string Text)> ProductionSources()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateDirectories(SourceRoot())
            .Where(project => Directory.EnumerateFiles(project, "*.csproj").Any())
            .Where(project =>
            {
                var name = Path.GetFileName(project);
                return !name.EndsWith("Tests", StringComparison.Ordinal) && name != "Cleansia.TestUtilities";
            })
            .SelectMany(project => Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !file.Contains($"{separator}Migrations{separator}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(file => (file, File.ReadAllText(file)));
    }

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.EnumerateFiles("Cleansia.Api.sln").Any())
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, $"Could not find Cleansia.Api.sln walking up from {AppContext.BaseDirectory}.");
        return dir!.FullName;
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider());

    /// <summary>A card checkout nobody has paid yet, still on the board's first status.</summary>
    private async Task SeedUnpaidCardOrderAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var order = Order.Create(
            customerName: "Token Customer",
            customerEmail: "token@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-token",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private sealed class FixedTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => TestTenants.Default;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
