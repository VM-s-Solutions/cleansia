using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// Owner ruling 2026-09-14: the erasure is ONE commit. Until then the refresh-token revoke inside
/// the walk committed the unit of work itself, so an erasure that failed after it left the subject
/// half-erased — documents and orders durable, the User row and the audit trail not — with no request on
/// record to say so. Against real Postgres, through <c>GdprDeletionService</c>'s real walk: a commit that
/// throws at the very end leaves the subject exactly as they were, their sessions still alive and no
/// <c>GdprRequest</c> written; a commit that lands revokes every session in the same write as the
/// subject's anonymisation.
/// </summary>
[Collection("PostgresCollection")]
public class ErasureSingleCommitTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SubjectId = TestConstants.TestUserSession.TestUserId;
    private const string BystanderId = "user-keep-session-1";
    private const string CountryId = "country-cz-erasure-commit";
    private const string CurrencyId = "currency-czk-erasure-1";
    private const string OrderId = "order-erasure-commit-1";
    private const string Description = "The kitchen floor was not mopped and the bins were left full.";

    [Fact]
    public async Task A_Throw_At_The_End_Of_The_Walk_Leaves_The_Subject_Untouched_Their_Sessions_Alive_And_No_Request_On_Record()
    {
        await TestMethod(
            arrange: Seed,
            act: async provider =>
            {
                var service = provider.GetRequiredService<IGdprDeletionService>();
                var context = provider.GetRequiredService<CleansiaDbContext>();

                var result = await service.DeleteUserAccountAsync(
                    SubjectId,
                    GdprAuditReasons.SelfDeletion,
                    user => (user.Email, null),
                    deferEmployeeErasure: true,
                    CancellationToken.None);
                Assert.True(result.IsSuccess);

                // Nothing the walk staged is durable yet. A row the commit cannot take (ActorId over its
                // 26-char column) makes the erasure's single SaveChangesAsync throw.
                context.AdminActionAudits.Add(new AdminActionAudit
                {
                    ActorId = new string('x', 40),
                    Action = "poison",
                    ActorProfile = UserProfile.Administrator,
                    Success = true,
                    TenantId = TestTenants.Default
                });

                await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.Equal(TestConstants.TestUserSession.TestUserEmail, user.Email);
                Assert.Equal(TestConstants.TestUserSession.TestFirstName, user.FirstName);
                Assert.True(user.IsActive);

                var tokens = await context.RefreshTokens.IgnoreQueryFilters().Where(t => t.UserId == SubjectId).ToListAsync();
                Assert.Equal(2, tokens.Count);
                Assert.All(tokens, t =>
                {
                    Assert.Null(t.RevokedAt);
                    Assert.True(t.IsAlive);
                });

                Assert.Empty(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());

                var consent = await context.UserConsents.IgnoreQueryFilters().SingleAsync(c => c.UserId == SubjectId);
                Assert.True(consent.IsGranted);

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(TestConstants.TestUserSession.TestUserEmail, order.CustomerEmail);

                var dispute = await context.Disputes.IgnoreQueryFilters().Include(d => d.Evidence).SingleAsync(d => d.OrderId == OrderId);
                Assert.Equal(Description, dispute.Description);
                Assert.Null(dispute.TextRetainedUntil);
                Assert.Equal("kitchen-floor.jpg", Assert.Single(dispute.Evidence).FileName);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Successful_Erasure_Revokes_Every_Session_In_The_Same_Commit_As_The_Subject()
    {
        await TestMethod(
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.StartsWith("deleted_", user.Email);
                Assert.False(user.IsActive);

                var tokens = await context.RefreshTokens.IgnoreQueryFilters().Where(t => t.UserId == SubjectId).ToListAsync();
                Assert.Equal(2, tokens.Count);
                Assert.All(tokens, t =>
                {
                    Assert.Equal(GdprAuditReasons.RefreshTokenRevocation, t.RevokedReason);
                    Assert.NotNull(t.RevokedAt);
                    Assert.False(t.IsAlive);
                });

                var bystanderToken = await context.RefreshTokens.IgnoreQueryFilters().SingleAsync(t => t.UserId == BystanderId);
                Assert.Null(bystanderToken.RevokedAt);

                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(SubjectId, request.UserId);
                Assert.Equal(GdprRequestStatus.Completed, request.Status);

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(AnonymizationMarker.Value, order.CustomerName);

                var dispute = await context.Disputes.IgnoreQueryFilters().Include(d => d.Evidence).SingleAsync(d => d.OrderId == OrderId);
                Assert.Equal(Description, dispute.Description);
                Assert.NotNull(dispute.TextRetainedUntil);
                Assert.Equal(AnonymizationMarker.Value, Assert.Single(dispute.Evidence).FilePath);
            });
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
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        subject.Id = SubjectId;
        subject.ConfirmEmail();
        context.Users.Add(subject);

        var bystander = User.CreateWithPassword("tomas.svoboda@cleansia.test", "Seed-Password-123", "Tomas", "Svoboda");
        bystander.Id = BystanderId;
        bystander.ConfirmEmail();
        context.Users.Add(bystander);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);

        context.RefreshTokens.AddRange(
            Token(SubjectId, "hash-subject-web", "Chrome 120 - macOS", deviceId: null),
            Token(SubjectId, "hash-subject-phone", "iPhone 15 / iOS 17.4", deviceId: "device-abc-123"),
            Token(BystanderId, "hash-bystander", "Pixel 8", deviceId: "device-keep-1"));

        context.UserConsents.Add(UserConsent.Grant(SubjectId, ConsentType.MarketingEmails, ipAddress: null, userAgent: null, documentVersion: null));

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
        dispute.AddMessage("Photos attached, the tiles are still grey.", SubjectId, isStaff: false);
        dispute.AddEvidence("kitchen-floor.jpg", $"{OrderId}/2f9c1a4b7d6e4f0b9c3a5e8d1f2b4c60.jpg", SubjectId);
        context.Disputes.Add(dispute);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static RefreshToken Token(string userId, string hash, string deviceLabel, string? deviceId)
    {
        var token = RefreshToken.Create(
            userId: userId,
            tokenHash: hash,
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            audience: JwtAudiences.Customer,
            deviceLabel: deviceLabel,
            ipAddress: "203.0.113.9",
            deviceId: deviceId);
        token.TenantId = TestTenants.Default;
        return token;
    }
}
