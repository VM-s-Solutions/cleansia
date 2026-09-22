using System.Text.Json;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Database;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// RECEIPTS ISSUED THE WAY PRODUCTION ISSUES THEM: the order booked through the real handler, written
/// to Postgres, and read back by the queue consumer in a scope of its own. Each money column is
/// <c>numeric(18,2)</c>, and the consumer's loader decides which lines and which account the document
/// can see, so an order held in memory proves neither.
///
/// <para>The prices are euro cents chosen so that rounding each term on its own misses the stored
/// total by one cent — 60.33 raw with a 20% promo on the express path, 8.10 raw with a 5% Plus
/// discount on the recurring one.</para>
/// </summary>
public partial class CreateOrderCallerCurrencyTests
{
    private const string ReceiptExtraSlug = "windows-reconcile";
    private const string ReceiptPromoCode = "CENTS20";
    private const string ReceiptSavedAddressId = "saved-reconcile";

    [Fact]
    public async Task An_Express_Booking_With_A_Promo_And_An_Extra_Is_Issued_A_Receipt_Whose_Lines_Sum_To_Its_Total()
    {
        var rendered = new List<ReceiptPdfData>();
        await TestMethod(
            setup: services => CaptureReceipts(services, rendered),
            arrange: async context =>
            {
                await SeedAsync(context);
                await PriceTheEuroServiceAtAsync(context, 58.33m);
                var extra = Extra.Create(ReceiptExtraSlug, "Windows", null);
                context.Extras.Add(extra);
                context.ExtraPrices.Add(ExtraPrice.Create(extra.Id, Eur, 2.00m));
                context.PromoCodes.Add(PromoCode.CreatePercent(ReceiptPromoCode, 0.20m));
                AddSlovakIssuer(context);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var created = await provider.GetRequiredService<IMediator>().Send(
                    BuildCommand(Slovakia, currencyId: null, totalPrice: (58.33m + 2.00m) * 1.2m) with
                    {
                        SelectedPackageIds = [],
                        Extras = new Dictionary<string, bool> { [ReceiptExtraSlug] = true },
                        CleaningDate = DateTime.UtcNow.AddHours(3),
                        PaymentType = PaymentType.Cash,
                        PromoCode = ReceiptPromoCode,
                    });
                Assert.True(created.IsSuccess, created.Error?.Message);

                await IssueReceiptInAFreshScopeAsync(provider, created.Value.Id);
                return created.Value.Id;
            },
            assert: (_, _) =>
            {
                var data = Assert.Single(rendered);
                var lines = new ReceiptLineProbe().Items(data);

                Assert.Equal(2.00m, Assert.Single(lines, l => l.Description == "Windows").Amount);
                Assert.Equal(12.07m, Assert.Single(lines, l => l.Description == "Express surcharge").Amount);
                Assert.Contains(lines, l => l.Description == "Promo code discount");
                Assert.Equal(data.Total, lines.Sum(l => l.Amount));
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Recurring_Occurrence_For_A_Plus_Member_Is_Issued_A_Receipt_Whose_Lines_Sum_To_Its_Total()
    {
        var rendered = new List<ReceiptPdfData>();
        await TestMethod(
            setup: services => CaptureReceipts(services, rendered),
            arrange: async context =>
            {
                await SeedAsync(context);
                await PriceTheEuroServiceAtAsync(context, 8.10m);
                var plan = MembershipPlan.Create("PLUS", "Plus", 5m, 4, true);
                context.MembershipPlans.Add(plan);
                context.UserMemberships.Add(UserMembership.Create(
                    CustomerUserId, plan.Id, Eur, "sub_reconcile", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMonths(1)));
                var address = Address.Create("Testovaci 12", "Bratislava", "11000", Slovakia);
                context.Addresses.Add(address);
                var saved = SavedAddress.Create(CustomerUserId, address.Id, "Home", false);
                saved.Id = ReceiptSavedAddressId;
                context.SavedAddresses.Add(saved);
                AddSlovakIssuer(context);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var starts = DateTime.UtcNow.AddDays(2).Date;
                var template = await provider.GetRequiredService<IMediator>().Send(new CreateRecurringBooking.Command(
                    (int)RecurrenceFrequency.Weekly, (int)starts.DayOfWeek, "10:00", 2, 1, ReceiptSavedAddressId,
                    [ServiceId], [], (int)PaymentType.Cash, starts));
                Assert.True(template.IsSuccess, template.Error?.Message);

                var materialized = await ActivatorUtilities.CreateInstance<MaterializeRecurringBookingTemplate.Handler>(provider)
                    .Handle(new MaterializeRecurringBookingTemplate.Command(template.Value.Id, DateTime.UtcNow, 7), CancellationToken.None);
                Assert.True(materialized.IsSuccess, materialized.Error?.Message);
                Assert.True(materialized.Value.OrdersCreated > 0);

                var orderIds = await provider.GetRequiredService<CleansiaDbContext>().Orders.IgnoreQueryFilters()
                    .Where(o => o.RecurringTemplateId == template.Value.Id)
                    .Select(o => o.Id)
                    .ToListAsync();
                foreach (var orderId in orderIds)
                {
                    await IssueReceiptInAFreshScopeAsync(provider, orderId);
                }

                return orderIds.Count;
            },
            assert: (_, occurrences) =>
            {
                Assert.Equal(occurrences, rendered.Count);
                Assert.All(rendered, data =>
                {
                    var lines = new ReceiptLineProbe().Items(data);
                    Assert.Contains(lines, l => l.Description == "Cleansia Plus discount");
                    Assert.Equal(data.Total, lines.Sum(l => l.Amount));
                });
                return Task.CompletedTask;
            },
            transactional: false);
    }

    /// <summary>
    /// A Google or Apple sign-up is stored as English and the web client never writes the preference
    /// back, so the account says English while the customer books in Czech. The order records the
    /// language its booking was made in, the consumer's loader reads it back from Postgres, and the
    /// receipt row records the result — whatever language the producer put on the message.
    /// </summary>
    [Fact]
    public async Task An_Account_Stored_As_English_That_Books_In_Czech_Is_Issued_A_Czech_Receipt()
    {
        var rendered = new List<ReceiptPdfData>();
        await TestMethod(
            setup: services => CaptureReceipts(services, rendered),
            arrange: async context =>
            {
                await SeedAsync(context);
                context.Languages.Add(Language.Create("cs", "Čeština"));
                (await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == CustomerUserId))
                    .UpdateLanguagePreference("en");
                AddSlovakIssuer(context);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var created = await provider.GetRequiredService<IMediator>().Send(
                    BuildCommand(Slovakia, currencyId: null, EurServicePrice + EurPackagePrice) with
                    {
                        PaymentType = PaymentType.Cash,
                        Language = "cs",
                    });
                Assert.True(created.IsSuccess, created.Error?.Message);

                await IssueReceiptInAFreshScopeAsync(provider, created.Value.Id);
                return created.Value.Id;
            },
            assert: async (context, orderId) =>
            {
                Assert.Equal("cs", Assert.Single(rendered).LanguageCode);
                Assert.Equal("cs", (await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId)).LanguageCode);
                var receipt = await context.OrderReceipts.IgnoreQueryFilters().SingleAsync(r => r.OrderId == orderId);
                Assert.Equal("cs", (await context.Languages.SingleAsync(l => l.Id == receipt.LanguageId)).Code);
            },
            transactional: false);
    }

    /// <summary>
    /// A guest has no account to read, and the card webhook that issues a paid guest's receipt passes
    /// English for everyone. The language the guest booked in comes back off the order.
    /// </summary>
    [Fact]
    public async Task A_Guest_Who_Books_In_Czech_Is_Issued_A_Czech_Receipt_From_An_English_Message()
    {
        var rendered = new List<ReceiptPdfData>();
        await TestMethod(
            setup: services => CaptureReceipts(services, rendered, ConfigureGuestSession),
            arrange: async context =>
            {
                await SeedAsync(context);
                context.Languages.Add(Language.Create("cs", "Čeština"));
                AddSlovakIssuer(context);
                StampUnstampedAdded(context, TestTenants.Default);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var created = await provider.GetRequiredService<IMediator>().Send(
                    BuildCommand(Slovakia, currencyId: null, EurServicePrice + EurPackagePrice) with
                    {
                        PaymentType = PaymentType.Cash,
                        Language = "cs",
                    });
                Assert.True(created.IsSuccess, created.Error?.Message);

                await IssueReceiptInAFreshScopeAsync(provider, created.Value.Id);
                return created.Value.Id;
            },
            assert: async (context, orderId) =>
            {
                Assert.Null((await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId)).UserId);
                Assert.Equal("cs", Assert.Single(rendered).LanguageCode);
            },
            transactional: false);
    }

    private static Task CaptureReceipts(
        IServiceCollection services,
        List<ReceiptPdfData> rendered,
        Func<IServiceCollection, Task>? session = null)
    {
        (session ?? ConfigureCustomerSession)(services);
        var bytes = new byte[] { 37, 80, 68, 70 };
        var pdf = new Mock<IPdfService>();
        pdf.Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Callback<ReceiptPdfData, string?>((data, _) => rendered.Add(data))
            .Returns(bytes);
        services.Replace(ServiceDescriptor.Singleton(pdf.Object));
        var blob = new Mock<IBlobContainerClient>();
        blob.Setup(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BlobFile(new MemoryStream(bytes), "application/pdf"));
        var blobs = new Mock<IBlobContainerClientFactory>();
        blobs.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blob.Object);
        services.Replace(ServiceDescriptor.Singleton(blobs.Object));
        services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => Mock.Of<IEmailService>()));
        return Task.CompletedTask;
    }

    /// <summary>
    /// A scope of its own, as the Functions host gives every queue message: the order is read back
    /// from Postgres by the consumer's loader, never handed over tracked from the booking.
    /// </summary>
    private static async Task IssueReceiptInAFreshScopeAsync(IServiceProvider provider, string orderId)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        await ActivatorUtilities.CreateInstance<GenerateReceiptHandler>(scope.ServiceProvider).HandleAsync(
            JsonSerializer.Serialize(
                new GenerateReceiptMessage(orderId, "en"),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            CancellationToken.None);
    }

    private static async Task PriceTheEuroServiceAtAsync(CleansiaDbContext context, decimal basePrice)
    {
        var euroPrice = await context.ServicePrices.SingleAsync(p => p.ServiceId == ServiceId && p.CurrencyId == Eur);
        euroPrice.Update(basePrice, 0m);
    }

    private static void AddSlovakIssuer(CleansiaDbContext context)
    {
        var issuer = CompanyInfo.Create("Slovak issuer", "SK", "SK123", "Street", "Bratislava", "11000", Slovakia);
        issuer.TenantId = TestTenants.Default;
        context.CompanyInfo.Add(issuer);
    }

    private sealed class ReceiptLineProbe : DefaultReceiptLayoutBuilder
    {
        public IReadOnlyList<(string Description, decimal Amount)> Items(ReceiptPdfData data) => ItemLines(data);
    }
}
