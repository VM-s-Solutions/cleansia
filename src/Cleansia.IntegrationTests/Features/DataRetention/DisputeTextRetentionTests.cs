using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.DataRetention;

/// <summary>
/// Owner ruling 2026-09-14 end to end on real Postgres: the customer's erasure through the real
/// command leaves the dispute's description and messages readable under a three-year stamp; the REAL
/// sweep (<c>RunAllRetentionTasksAsync</c>, the wiring the Functions timer runs) leaves them alone while
/// the stamp is in the future, and blanks them — and clears the stamp — once it is past. The stamp is
/// moved into the past with a raw update, the only honest way to age a row without waiting three years.
/// </summary>
[Collection("PostgresCollection")]
public class DisputeTextRetentionTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SubjectId = TestConstants.TestUserSession.TestUserId;
    private const string AdminId = "user-admin-dispute-text";
    private const string CountryId = "country-cz-dispute-text";
    private const string CurrencyId = "currency-czk-dispute-text";
    private const string OrderId = "order-dispute-text-1";
    private const string Description = "The kitchen floor was not mopped and the bins were left full.";
    private const string CustomerMessage = "Photos attached, the tiles are still grey.";
    private const string StaffMessage = "We are sorry — a partial refund is on its way.";

    [Fact]
    public async Task After_Erasure_The_Text_Is_Readable_Under_The_Stamp_And_The_Sweep_Leaves_It_Until_The_Stamp_Is_Past()
    {
        await TestMethod(
            setup: RegisterSweep,
            arrange: Seed,
            act: async provider =>
            {
                var erased = await provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command());
                Assert.True(erased.IsSuccess, erased.Error?.Message);

                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var dispute = await ReadDispute(context);
                Assert.Equal(Description, dispute.Description);
                Assert.Equal([CustomerMessage, StaffMessage], dispute.Messages.OrderBy(m => m.CreatedOn).Select(m => m.Message));
                Assert.NotNull(dispute.TextRetainedUntil);
                Assert.InRange(
                    dispute.TextRetainedUntil!.Value,
                    DateTimeOffset.UtcNow.AddYears(RetentionDefaults.DefaultDisputeTextRetentionYears).AddMinutes(-5),
                    DateTimeOffset.UtcNow.AddYears(RetentionDefaults.DefaultDisputeTextRetentionYears));
            },
            transactional: false);
    }

    [Fact]
    public async Task Once_The_Stamp_Is_Past_The_Sweep_Blanks_The_Text_And_Clears_The_Stamp()
    {
        await TestMethod(
            setup: RegisterSweep,
            arrange: Seed,
            act: async provider =>
            {
                var erased = await provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command());
                Assert.True(erased.IsSuccess, erased.Error?.Message);

                var context = provider.GetRequiredService<CleansiaDbContext>();
                await context.Database.ExecuteSqlAsync(
                    $"""UPDATE "Disputes" SET "TextRetainedUntil" = {DateTimeOffset.UtcNow.AddDays(-1)} WHERE "OrderId" = {OrderId}""");

                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var dispute = await ReadDispute(context);
                Assert.Equal(AnonymizationMarker.Value, dispute.Description);
                Assert.All(dispute.Messages, m => Assert.Equal(AnonymizationMarker.Value, m.Message));
                Assert.Null(dispute.TextRetainedUntil);
                Assert.Equal(DisputeStatus.Pending, dispute.Status);
                Assert.Equal(SubjectId, dispute.UserId);
            },
            transactional: false);
    }

    private static Task RegisterSweep(IServiceCollection services)
    {
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
        services.Replace(ServiceDescriptor.Singleton(_ => new Mock<IBlobContainerClientFactory>().Object));
        return Task.CompletedTask;
    }

    private static Task<Dispute> ReadDispute(CleansiaDbContext context) =>
        context.Disputes.IgnoreQueryFilters().Include(d => d.Messages).SingleAsync(d => d.OrderId == OrderId);

    private static async Task Seed(CleansiaDbContext context)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
        }

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var subject = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        subject.Id = SubjectId;
        subject.ConfirmEmail();
        context.Users.Add(subject);

        var admin = User.CreateWithPassword("support@cleansia.test", "Seed-Password-123", "Support", "Desk", UserProfile.Administrator);
        admin.Id = AdminId;
        admin.ConfirmEmail();
        context.Users.Add(admin);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);

        var order = Order.Create(
            customerName: $"{TestConstants.TestUserSession.TestFirstName} {TestConstants.TestUserSession.TestLastName}",
            customerEmail: TestConstants.TestUserSession.TestUserEmail,
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-3),
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: SubjectId);
        order.Id = OrderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        context.Orders.Add(order);

        var dispute = new Dispute(OrderId, SubjectId, DisputeReason.QualityIssue, Description, SubjectId);
        dispute.AddMessage(CustomerMessage, SubjectId, isStaff: false);
        dispute.AddMessage(StaffMessage, AdminId, isStaff: true);
        context.Disputes.Add(dispute);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }
}
