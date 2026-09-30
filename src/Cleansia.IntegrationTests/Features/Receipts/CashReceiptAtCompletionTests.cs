using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Database;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Receipts;

/// <summary>
/// Owner ruling 2026-09-28, end to end over Postgres: the assigned cleaner records the cash through the
/// real pipeline, which stamps the amount due; the cleaner completes the clean through it too, which
/// queues the receipt; and the queue consumer, reading that message in a scope of its own, issues the one
/// receipt the sale gets — stating the booked slot, the completion and the cash handover in the market's
/// time. Until the cash is recorded there is nothing to receipt.
/// </summary>
[Collection("PostgresCollection")]
public class CashReceiptAtCompletionTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "01K5RCPTCASH0AFTERD0NE0001";
    private const string CurrencyId = "currency-czk-cash-done";
    private const string CountryId = "country-cz-cash-done";
    private const string CleanerUserId = "user-cash-completion";
    private const string CleanerEmployeeId = "employee-cash-completion";
    private const string CleanerEmail = "cleaner-cash-completion@cleansia.test";
    private static readonly TimeZoneInfo Prague = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");

    [Fact]
    public async Task The_Collected_And_Completed_Cash_Sale_Is_Receipted_With_All_Three_Times_In_Market_Time()
    {
        var rendered = new List<ReceiptPdfData>();
        var cleaning = new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc);
        await TestMethod(
            setup: services => AssignedCleanerWithCapturedReceipts(services, rendered),
            arrange: context => SeedCashOrderInProgressAsync(context, cleaning),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var collected = await mediator.Send(new MarkCashCollected.Command(OrderId));
                Assert.True(collected.IsSuccess, collected.Error?.Message);

                var completed = await mediator.Send(new CompleteOrder.Command(OrderId));
                Assert.True(completed.IsSuccess, completed.Error?.Message);

                await HandleQueuedReceiptAsync(provider);
                return true;
            },
            assert: async (context, _) =>
            {
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
                Assert.Equal(CleanerEmployeeId, order.CollectedByEmployeeId);
                Assert.Equal(1500m, order.CashCollectedAmount);

                var data = Assert.Single(rendered);
                Assert.Equal("15.07.2026 10:00", data.CleaningDate);
                Assert.Equal(MarketTime(order.CompletedAt!.Value), data.CompletedAt);
                Assert.Equal(MarketTime(order.CashCollectedAt!.Value), data.CashReceivedAt);
                Assert.Equal(PaymentStatus.Paid, data.PaymentStatus);
                Assert.True(await context.OrderReceipts.IgnoreQueryFilters().AnyAsync(r => r.OrderId == OrderId));
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Cash_Booking_Whose_Cash_Was_Not_Recorded_Gets_No_Receipt()
    {
        var rendered = new List<ReceiptPdfData>();
        await TestMethod(
            setup: services => AssignedCleanerWithCapturedReceipts(services, rendered),
            arrange: context => SeedCashOrderInProgressAsync(context, DateTime.UtcNow),
            act: async provider =>
            {
                await HandleReceiptMessageAsync(provider);
                return true;
            },
            assert: async (context, _) =>
            {
                Assert.Empty(rendered);
                Assert.False(await context.OrderReceipts.IgnoreQueryFilters().AnyAsync(r => r.OrderId == OrderId));
            },
            transactional: false);
    }

    private static string MarketTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Prague)
            .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The message the completion queued, handled in a scope of its own as the Functions host does.</summary>
    private static async Task HandleQueuedReceiptAsync(IServiceProvider provider)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var receiptKey = MessageKeys.Receipt(OrderId);
        var queued = await scope.ServiceProvider.GetRequiredService<CleansiaDbContext>().OutboxMessages
            .IgnoreQueryFilters()
            .SingleAsync(m => m.QueueName == QueueNames.GenerateReceipt && m.MessageKey == receiptKey);
        await ActivatorUtilities.CreateInstance<GenerateReceiptHandler>(scope.ServiceProvider)
            .HandleAsync(queued.Body, CancellationToken.None);
    }

    /// <summary>A scope of its own, as the Functions host gives every queue message.</summary>
    private static async Task HandleReceiptMessageAsync(IServiceProvider provider)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        await ActivatorUtilities.CreateInstance<GenerateReceiptHandler>(scope.ServiceProvider)
            .HandleAsync(JsonSerializer.Serialize(
                new GenerateReceiptMessage(OrderId, "en"),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), CancellationToken.None);
    }

    private static Task AssignedCleanerWithCapturedReceipts(IServiceCollection services, List<ReceiptPdfData> rendered)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CleanerUserId,
            CleanerEmail,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, CleanerEmployeeId),
            ])));

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

    private static async Task SeedCashOrderInProgressAsync(CleansiaDbContext context, DateTime cleaningDateTime)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(CountryConfiguration.Create(
            CountryId, "CZK", "cs", 0.21m, timeZoneId: "Europe/Prague"));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        context.Currencies.Add(currency);

        var issuer = CompanyInfo.Create("Cleansia s.r.o.", "Cleansia", "12345678", "Hlavní 1", "Praha", "11000", CountryId);
        issuer.TenantId = TestTenants.Default;
        context.CompanyInfo.Add(issuer);

        var cleanerUser = User.CreateWithPassword(
            CleanerEmail, TestConstants.TestUserSession.TestUserPassword, "Petra", "Svobodova", UserProfile.Employee);
        cleanerUser.Id = CleanerUserId;
        cleanerUser.ConfirmEmail();
        context.Users.Add(cleanerUser);
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerEmployeeId;
        cleaner.Approve(approvedByUserId: "admin-cash-completion");
        cleaner.AssignWorkCountry(CountryId);
        cleaner.UpdateAddress(Address.Create("Korunni 9", "Praha", "12000", CountryId));
        context.Employees.Add(cleaner);

        var order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: "guest-cash-completion@cleansia.test",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: null);
        order.Id = OrderId;
        order.UpdateEstimatedTime(120);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        var seat = OrderEmployee.Create(order, cleaner);
        order.AddAssignedEmployee(seat);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.InProgress, order));
        context.Orders.Add(order);

        var (contract, _) = TestLegalDocuments.Add(context);
        context.WorkContractAcceptances.Add(WorkContractAcceptance.Create(
            OrderId, seat.Id, CleanerEmployeeId, contract.TextFor("en")!, contract.Version, "cleansia.mobile",
            ipAddress: null, deviceLabel: null, deviceId: null, factsJson: "{}"));
        context.Add(OrderPhoto.Create(
            orderId: OrderId,
            photoType: PhotoType.After,
            blobUrl: "https://account.blob.core.windows.net/order-photos/cash-done/after.jpg",
            fileName: "after.jpg",
            originalFileName: "after.jpg",
            fileSizeBytes: 2048,
            contentType: "image/jpeg",
            capturedByEmployeeId: CleanerEmployeeId));

        await context.CommitAsync(CancellationToken.None);
    }
}
