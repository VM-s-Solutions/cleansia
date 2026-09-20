using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// ADR-0064 D3 (TC-LC-ARCH-5) — every stamped table is sorted, deliberately, as the person's
/// (the account surface the freeze lets through) or the company's (the books it refuses). The
/// guard treats everything not on the account surface as books, so an unsorted new table fails
/// closed at runtime; this makes the sorting a build-time act with a one-line reason, by walking
/// <c>ctx.Model</c> rather than a copied list. The residual the guard cannot see — a tenantless child
/// of a books row, written beside its parent — is named here by type, so the next one is sorted on
/// purpose rather than found by accident.
/// </summary>
public sealed class ArchivedCompanyWriteGuardRosterTests : IDisposable
{
    /// <summary>The company's books: refused once the company is frozen. One line per type, so the sort is read, not inferred.</summary>
    private static readonly HashSet<Type> Books =
    [
        typeof(Address),
        typeof(Cleansia.Core.Domain.Company.CompanyInfo),
        typeof(CreditAccount),
        typeof(Dispute),
        typeof(Cleansia.Core.Domain.Documents.DocumentDeletionRequest),
        typeof(Employee),
        typeof(Cleansia.Core.Domain.Documents.EmployeeDocument),
        typeof(Cleansia.Core.Domain.EmployeePayroll.EmployeeInvoice),
        typeof(Cleansia.Core.Domain.EmployeePayroll.EmployeePayConfig),
        typeof(EmployeePayoutDetails),
        typeof(Cleansia.Core.Domain.Receipts.FiscalCounter),
        typeof(Order),
        typeof(Cleansia.Core.Domain.EmployeePayroll.OrderEmployeePay),
        typeof(OrderIssue),
        typeof(OrderNote),
        typeof(OrderPhoto),
        typeof(Cleansia.Core.Domain.Receipts.OrderReceipt),
        typeof(OrderReview),
        typeof(OrderStatusTrack),
        typeof(Cleansia.Core.Domain.EmployeePayroll.PayPeriod),
        typeof(Cleansia.Core.Domain.EmployeePayroll.PayoutReferenceCounter),
        typeof(Cleansia.Core.Domain.Loyalty.PromoCode),
        typeof(Cleansia.Core.Domain.Loyalty.PromoCodeRedemption),
        typeof(Cleansia.Core.Domain.Bookings.RecurringBookingTemplate),
        typeof(Cleansia.Core.Domain.Payments.Refund),
        typeof(Cleansia.Core.Domain.Configuration.TenantConfiguration),
        // A contract record for a retained order: a frozen company forms no contracts, and the archive
        // preconditions leave no open order, so the guard is belt rather than path here.
        typeof(Cleansia.Core.Domain.Contracts.WorkContractAcceptance),
    ];

    /// <summary>
    /// Tenantless children of books rows: no <c>TenantId</c>, so a commit adding only such a row
    /// passes the guard. Bounded because each is written beside a parent write the guard sees, or
    /// through a path the freeze closed upstream (no open order, no open dispute, no credit balance,
    /// no cleaner who can sign in). The guard does not walk foreign keys for them.
    /// </summary>
    private static readonly HashSet<Type> TenantlessChildrenOfBooks =
    [
        typeof(DisputeLine),
        typeof(CreditTransaction),
        typeof(OrderReviewLine),
        typeof(OrderExtra),
        typeof(DisputeMessage),
        typeof(DisputeEvidence),
        typeof(OrderEmployee),
        typeof(OrderService),
        typeof(OrderPackage),
    ];

    private readonly SqliteConnection _connection;

    public ArchivedCompanyWriteGuardRosterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options, new TestUserSessionProvider("system", "system@cleansia.test"), new FixedTenantProvider());
    }

    private sealed class FixedTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => TestTenants.Default;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }

    [Fact]
    public void Every_Stamped_Type_Is_Sorted_As_Account_Surface_Or_Books_And_Never_Both()
    {
        using var ctx = NewContext();
        var stamped = ctx.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => typeof(ITenantEntity).IsAssignableFrom(t))
            .ToList();

        var unsorted = stamped
            .Where(t => !ArchivedCompanyWriteGuard.AccountSurface.Contains(t) && !Books.Contains(t))
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        var sortedTwice = stamped
            .Where(t => ArchivedCompanyWriteGuard.AccountSurface.Contains(t) && Books.Contains(t))
            .Select(t => t.Name)
            .ToList();
        var notInModel = ArchivedCompanyWriteGuard.AccountSurface.Concat(Books)
            .Where(t => !stamped.Contains(t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(stamped.Count >= 40, $"Only {stamped.Count} stamped entities were walked; the roster-free walk is not seeing the model.");
        Assert.True(unsorted.Count == 0,
            "ADR-0064 D3: a new stamped table is books until sorted. Add it to ArchivedCompanyWriteGuard.AccountSurface "
            + "(the person's) with a reason, or to this test's Books roster (the company's):\n  " + string.Join("\n  ", unsorted));
        Assert.Empty(sortedTwice);
        Assert.Empty(notInModel);
    }

    [Fact]
    public void The_Account_Surface_Is_The_Decision_Text_Verbatim_And_Credit_Is_Books()
    {
        var expected = new[]
        {
            "AdminActionAudit", "CustomerActionAudit", "DeadLetter", "Device", "EmployeeActionAudit", "GdprRequest",
            "LiveActivityToken", "LoyaltyAccount", "LoyaltyTransaction", "MembershipBenefitUsage", "OutboxMessage", "Referral",
            "ReferralCode", "RefreshToken", "SavedAddress", "User", "UserConsent", "UserMembership", "UserNotification",
            "UserNotificationPreferences", "UserStripeCustomer",
        };

        Assert.Equal(expected, ArchivedCompanyWriteGuard.AccountSurface.Select(t => t.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(typeof(CreditAccount), ArchivedCompanyWriteGuard.AccountSurface);
    }

    /// <summary>
    /// A type without a tenant column whose foreign key points at a books type is a row the guard
    /// cannot see. The four the decision names, and the five the model also holds, are listed above;
    /// a new one fails here so it is sorted on purpose.
    /// </summary>
    [Fact]
    public void Every_Tenantless_Child_Of_A_Books_Type_Is_Named()
    {
        using var ctx = NewContext();
        var found = ctx.Model.GetEntityTypes()
            .Where(e => !typeof(ITenantEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetForeignKeys().Any(fk => Books.Contains(fk.PrincipalEntityType.ClrType)))
            .Select(e => e.ClrType)
            .ToHashSet();

        var unnamed = found.Except(TenantlessChildrenOfBooks).Select(t => t.Name).Order(StringComparer.Ordinal).ToList();
        var stale = TenantlessChildrenOfBooks.Except(found).Select(t => t.Name).Order(StringComparer.Ordinal).ToList();

        Assert.True(unnamed.Count == 0,
            "ADR-0064 D3: a tenantless row with a foreign key into the books passes the write guard. Name it in "
            + "TenantlessChildrenOfBooks with the reason its write is bounded:\n  " + string.Join("\n  ", unnamed));
        Assert.Empty(stale);
        foreach (var named in new[] { typeof(DisputeLine), typeof(CreditTransaction), typeof(OrderReviewLine), typeof(OrderExtra) })
        {
            Assert.Contains(named, found);
        }
    }

    [Fact]
    public void The_Guard_Reads_Only_Changed_Stamped_Rows_Outside_The_Account_Surface()
    {
        using var ctx = NewContext();
        ctx.Database.EnsureCreated();

        var refreshToken = RefreshToken.Create("user-1", "hash", DateTimeOffset.UtcNow.AddDays(1), "cleansia.customer", null, null);
        refreshToken.TenantId = "cleansia-a";
        var receipt = Cleansia.Core.Domain.Receipts.OrderReceipt.Create("order-1", "RCP-2026-0001", "r.pdf", "r.pdf", "en");
        receipt.TenantId = "cleansia-b";
        var untouchedRefund = Cleansia.Core.Domain.Payments.Refund.Create(
            "order-2", "refund:x", 10m, "CZK", Cleansia.Core.Domain.Enums.RefundReason.CustomerCancellation, Cleansia.Core.Domain.Enums.RefundSource.AppRefund);
        untouchedRefund.TenantId = "cleansia-c";

        ctx.Add(refreshToken);
        ctx.Add(receipt);
        ctx.Attach(untouchedRefund);

        Assert.Equal(["cleansia-b"], ArchivedCompanyWriteGuard.TouchedBooksTenantIds(ctx.ChangeTracker));
    }
}
