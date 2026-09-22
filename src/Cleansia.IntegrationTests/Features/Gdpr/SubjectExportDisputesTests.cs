using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// The subject export's disputes section (owner ruling 2026-09-15: "add it") against real Postgres,
/// through the real pipeline. A dispute is the subject's when it is filed on the account or sits on one
/// of <c>SubjectOrders</c> — the same order set the orders section lists — and a stranger's is not.
/// Each carries the order it is on, the reason and status as names, the text, the resolution with its
/// refund in the order's currency, every message with its author role, and the evidence file names.
/// After an erasure the orders no longer name the account and the dispute is reached through the
/// account it was filed on; the section reads what is stored — the evidence names are the marker at
/// once, the text until the three-year window closes, and the marker after the sweep. The export
/// fabricates nothing and hides nothing.
/// </summary>
[Collection("PostgresCollection")]
public class SubjectExportDisputesTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SubjectId = TestConstants.TestUserSession.TestUserId;
    private const string SubjectEmail = TestConstants.TestUserSession.TestUserEmail;
    private const string StrangerId = "user-xd-stranger";
    private const string StrangerEmail = "tomas.svoboda@cleansia.test";
    private const string AdminId = "admin-xd";
    private const string AdminEmail = "admin-xd@cleansia.test";
    private const string CountryId = "country-cz-xd";
    private const string CurrencyId = "currency-czk-xd";
    private const string OwnOrderId = "order-xd-own";
    private const string GuestOrderId = "order-xd-guest";
    private const string StrangerOrderId = "order-xd-stranger";
    private const string OwnDisputeId = "dispute-xd-own";
    private const string StrangerDisputeId = "dispute-xd-stranger";
    private const string Description = "The kitchen floor was not mopped and the bins were left full.";
    private const string CustomerMessage = "Photos attached, the tiles are still grey.";
    private const string StaffMessage = "We have reviewed the photos and will refund part of the job.";
    private const string ResolutionNotes = "Partial refund for the kitchen.";
    private const string EvidenceFileName = "kitchen-floor.jpg";
    private const string StrangerDescription = "Nobody came on Tuesday.";

    private static Task AsTheSubject(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            SubjectId, SubjectEmail, [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ => new TestRequestMetadataProvider("203.0.113.9", "iPhone 15 / iOS 17.4")));
        return Task.CompletedTask;
    }

    private static Task AsAdministratorWithoutBlobStorage(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            AdminId, AdminEmail, [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));

        // The evidence blobs are the storage account's; here the rows are the question, and the real
        // client would spend the SDK's retry budget on an emulator that is not there.
        var factory = new Mock<IBlobContainerClientFactory>();
        factory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(Mock.Of<IBlobContainerClient>());
        services.Replace(ServiceDescriptor.Singleton(_ => factory.Object));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_Live_Subjects_Export_Lists_Their_Dispute_With_Its_Order_Text_Messages_Evidence_And_Refund_And_Not_A_Strangers()
    {
        await TestMethod(
            setup: AsTheSubject,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new ExportUserData.Command()),
            assert: async (CleansiaDbContext context, BusinessResult<GdprExportDto> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                Assert.Equal([GuestOrderId, OwnOrderId], result.Value.Orders.Select(o => o.Id).Order());
                Assert.DoesNotContain(result.Value.Disputes, d => d.Id == StrangerDisputeId);
                Assert.DoesNotContain(result.Value.Disputes, d => d.Description == StrangerDescription);

                var ownNumber = await context.Orders.IgnoreQueryFilters().Where(o => o.Id == OwnOrderId).Select(o => o.DisplayOrderNumber).SingleAsync();
                var own = Assert.Single(result.Value.Disputes);
                Assert.Equal(OwnDisputeId, own.Id);
                Assert.Equal(OwnOrderId, own.OrderId);
                Assert.Equal(ownNumber, own.OrderDisplayNumber);
                Assert.Equal(nameof(DisputeReason.QualityIssue), own.Reason);
                Assert.Equal(Description, own.Description);
                Assert.Equal(nameof(DisputeStatus.Resolved), own.Status);
                Assert.Equal(ResolutionNotes, own.ResolutionNotes);
                Assert.Equal(300m, own.RefundAmount);
                Assert.Equal("CZK", own.CurrencyCode);
                Assert.NotEqual(default, own.CreatedOn);
                Assert.NotNull(own.ResolvedOn);
                Assert.Equal(["Customer", "Staff"], own.Messages.Select(m => m.AuthorRole));
                Assert.Equal([CustomerMessage, StaffMessage], own.Messages.Select(m => m.Text));
                Assert.All(own.Messages, m => Assert.NotEqual(default, m.SentAt));
                Assert.Equal([EvidenceFileName], own.EvidenceFileNames);

                var audit = Assert.Single(await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync(), a => a.Action == "customer.gdpr.export");
                var evidence = JsonDocument.Parse(audit.PayloadJson!).RootElement;
                Assert.Equal(2, evidence.GetProperty("orderCount").GetInt32());
                Assert.Equal(1, evidence.GetProperty("disputeCount").GetInt32());
                Assert.DoesNotContain("kitchen", audit.PayloadJson!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(EvidenceFileName, audit.PayloadJson!);
            },
            transactional: false);
    }

    [Fact]
    public async Task An_Erased_Subjects_Export_Still_Lists_The_Dispute_Filed_On_The_Account_Reading_The_Marker_Where_The_Sweep_Has_Blanked_And_The_Text_Where_It_Has_Not()
    {
        await TestMethod(
            setup: AsAdministratorWithoutBlobStorage,
            arrange: Seed,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var erased = await mediator.Send(new AdminDeleteUserAccount.Command(SubjectId));
                Assert.True(erased.IsSuccess, erased.Error?.Message);
                var retained = await mediator.Send(new AdminExportUserData.Command(SubjectId));

                // The retention sweep's work, done by hand: the window has closed on this dispute.
                var context = provider.GetRequiredService<CleansiaDbContext>();
                var dispute = await context.Disputes.IgnoreQueryFilters().Include(d => d.Messages).Include(d => d.Evidence).SingleAsync(d => d.Id == OwnDisputeId);
                dispute.Anonymize();
                await context.SaveChangesAsync();

                var swept = await mediator.Send(new AdminExportUserData.Command(SubjectId));
                return (retained, swept);
            },
            assert: async (CleansiaDbContext context, (BusinessResult<GdprExportDto> Retained, BusinessResult<GdprExportDto> Swept) outcome) =>
            {
                var (retained, swept) = outcome;
                Assert.True(retained.IsSuccess, retained.Error?.Message);
                Assert.True(swept.IsSuccess, swept.Error?.Message);

                // The erasure took the account off its orders, so the orders section is empty and the
                // dispute is reached through the account it was filed on.
                Assert.Empty(retained.Value.Orders);
                Assert.StartsWith("deleted_", retained.Value.Profile.Email);

                var ownNumber = await context.Orders.IgnoreQueryFilters().Where(o => o.Id == OwnOrderId).Select(o => o.DisplayOrderNumber).SingleAsync();
                var own = Assert.Single(retained.Value.Disputes);
                Assert.Equal(OwnDisputeId, own.Id);
                Assert.Equal(ownNumber, own.OrderDisplayNumber);
                Assert.Equal(Description, own.Description);
                Assert.Equal(ResolutionNotes, own.ResolutionNotes);
                Assert.Equal([CustomerMessage, StaffMessage], own.Messages.Select(m => m.Text));
                Assert.Equal([AnonymizationMarker.Value], own.EvidenceFileNames);
                Assert.Equal(300m, own.RefundAmount);
                Assert.Equal("CZK", own.CurrencyCode);

                var blanked = Assert.Single(swept.Value.Disputes);
                Assert.Equal(OwnDisputeId, blanked.Id);
                Assert.Equal(AnonymizationMarker.Value, blanked.Description);
                Assert.Equal(AnonymizationMarker.Value, blanked.ResolutionNotes);
                Assert.Equal([AnonymizationMarker.Value, AnonymizationMarker.Value], blanked.Messages.Select(m => m.Text));
                Assert.Equal(["Customer", "Staff"], blanked.Messages.Select(m => m.AuthorRole));
                Assert.Equal([AnonymizationMarker.Value], blanked.EvidenceFileNames);
                Assert.Equal(nameof(DisputeReason.QualityIssue), blanked.Reason);
                Assert.Equal(nameof(DisputeStatus.Resolved), blanked.Status);
                Assert.Equal(300m, blanked.RefundAmount);

                var exports = await context.AdminActionAudits.IgnoreQueryFilters().Where(a => a.Action == "gdpr.user.export").ToListAsync();
                Assert.Equal(2, exports.Count);
                Assert.All(exports, a =>
                {
                    Assert.Equal(1, JsonDocument.Parse(a.AfterJson!).RootElement.GetProperty("disputeCount").GetInt32());
                    Assert.DoesNotContain("kitchen", a.AfterJson!, StringComparison.OrdinalIgnoreCase);
                });
            },
            transactional: false);
    }

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
            email: SubjectEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        subject.Id = SubjectId;
        subject.ConfirmEmail();
        var stranger = User.CreateWithPassword(StrangerEmail, "Seed-Password-123", "Tomas", "Svoboda");
        stranger.Id = StrangerId;
        stranger.ConfirmEmail();
        var admin = User.CreateWithPassword(AdminEmail, "Seed-Password-123", "Ad", "Min", UserProfile.Administrator, adminRole: AdminRole.Administrator);
        admin.Id = AdminId;
        admin.ConfirmEmail();
        context.Users.AddRange(subject, stranger, admin);

        context.Orders.AddRange(
            NewOrder(OwnOrderId, SubjectId, SubjectEmail),
            NewOrder(StrangerOrderId, StrangerId, StrangerEmail));

        var own = new Dispute(OwnOrderId, SubjectId, DisputeReason.QualityIssue, Description, SubjectId);
        own.Id = OwnDisputeId;
        own.AddMessage(CustomerMessage, SubjectId, isStaff: false);
        own.AddMessage(StaffMessage, AdminId, isStaff: true);
        // Two messages added in the same instant tie on CreatedOn, and the export then orders them by
        // a random id — a minute apart is the conversation the assertion reads back.
        var messages = own.Messages.ToList();
        context.Entry(messages[0]).Property(nameof(DisputeMessage.CreatedOn)).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-2);
        context.Entry(messages[1]).Property(nameof(DisputeMessage.CreatedOn)).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);
        own.AddEvidence(EvidenceFileName, $"{OwnOrderId}/2f9c1a4b7d6e4f0b9c3a5e8d1f2b4c60.jpg", SubjectId);
        own.Resolve(AdminId, 300m, ResolutionNotes);

        var strangers = new Dispute(StrangerOrderId, StrangerId, DisputeReason.ServiceNotProvided, StrangerDescription, StrangerId);
        strangers.Id = StrangerDisputeId;
        context.Disputes.AddRange(own, strangers);
        StampUnstampedAdded(context, TestTenants.Default);

        // The subject's guest booking, placed in the second operator's market and typed in capitals: one
        // of the orders section's, with no dispute of its own — a dispute is filed on an account.
        context.Orders.Add(NewOrder(GuestOrderId, userId: null, SubjectEmail.ToUpperInvariant()));
        StampUnstampedAdded(context, TestTenants.Second);

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(string id, string? userId, string customerEmail)
    {
        var order = Order.Create(
            customerName: $"{TestConstants.TestUserSession.TestFirstName} {TestConstants.TestUserSession.TestLastName}",
            customerEmail: customerEmail,
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-30),
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = id;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        return order;
    }
}
