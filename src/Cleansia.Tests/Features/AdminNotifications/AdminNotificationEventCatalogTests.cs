using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.Domain.Notifications;

namespace Cleansia.Tests.Features.AdminNotifications;

/// <summary>
/// The admin catalogue is one set of keys seen from three places — the Domain constants, the
/// AppServices table and the feed audience — and the three must be the same set: a key in one and
/// not another is a row the console counts and cannot render, or an event nobody can be told about.
/// The keys are disjoint from the two client keysets (a dual-role user's rows partition per host),
/// prefixed <c>admin.</c>, non-mutable, and never reach the push seam. Every arg name an event may
/// carry is drawn from a closed vocabulary of ids, numbers, enum names, dates and money — never a
/// person — because <c>ArgsJson</c> is shown to every administrator of the company.
/// </summary>
public sealed class AdminNotificationEventCatalogTests
{
    private static readonly IReadOnlySet<string> AllowedArgNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "orderId", "orderNumber", "amount", "paymentType", "countryId",
        "cause", "statusAtLoss", "cleaningDateTime",
        "disputeId", "reason",
        "requestId", "day",
        "windDownFrom", "cancelled", "refunded", "refundFailures", "periodsClosed",
        "archivedOn",
    };

    private static readonly Regex IdentityOrFreeText = new(
        "(name|email|phone|address|street|city|zip|zipcode|iban|token|secret|password|description|instructions?|note|notes|comment|message|text)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static IReadOnlyList<string> DomainConstants() =>
        typeof(AdminNotificationEventCatalog)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    [Fact]
    public void All_Lists_Every_Domain_Constant_Once()
    {
        Assert.Equal(DomainConstants().OrderBy(k => k), AdminNotificationEventCatalog.All.OrderBy(k => k));
        Assert.Equal(AdminNotificationEventCatalog.All.Count, AdminNotificationEventCatalog.All.Distinct().Count());
    }

    [Fact]
    public void The_Domain_Keys_The_AppServices_Table_And_The_Feed_Keyset_Are_One_Set()
    {
        Assert.Equal(AdminNotificationEventCatalog.All, AdminEventCatalog.All.Select(e => e.Key).ToList());
        Assert.Same(AdminNotificationEventCatalog.All, NotificationFeedEventKeys.Admin);
        Assert.Same(NotificationFeedEventKeys.Admin, NotificationFeedEventKeys.For(NotificationFeedAudience.Admin));
    }

    [Fact]
    public void Every_Key_Is_Prefixed_Admin_And_Disjoint_From_The_Client_Keysets()
    {
        Assert.All(AdminNotificationEventCatalog.All, key => Assert.StartsWith("admin.", key, StringComparison.Ordinal));
        Assert.Empty(AdminNotificationEventCatalog.All.Intersect(NotificationFeedEventKeys.Customer));
        Assert.Empty(AdminNotificationEventCatalog.All.Intersect(NotificationFeedEventKeys.Partner));
    }

    [Fact]
    public void Every_Key_Is_Non_Mutable_And_Unknown_To_The_Push_Seam()
    {
        foreach (var key in AdminNotificationEventCatalog.All)
        {
            Assert.Null(NotificationEventCatalog.GetCategoryFor(key));
            Assert.False(NotificationFeedEventKeys.IsFeedEvent(key),
                $"{key} must not be a push-seam feed key: NotificationProducer would write it and enqueue a push.");
        }
    }

    // ADR-0066 D8: every audience is the NAME of an administrator set, and the one event that belongs to
    // two branches of the lattice — a chargeback — is any administrator.
    [Theory]
    [InlineData(AdminNotificationEventCatalog.OrderNew, PhysicalPolicy.SupportOrAbove)]
    [InlineData(AdminNotificationEventCatalog.OrderCrewLost, PhysicalPolicy.SupportOrAbove)]
    [InlineData(AdminNotificationEventCatalog.DisputeFiled, PhysicalPolicy.SupportOrAbove)]
    [InlineData(AdminNotificationEventCatalog.DisputeChargeback, PhysicalPolicy.AdminOnly)]
    [InlineData(AdminNotificationEventCatalog.PaymentFailed, PhysicalPolicy.SupportOrAbove)]
    [InlineData(AdminNotificationEventCatalog.ErasureFailed, PhysicalPolicy.ManagerOrAbove)]
    [InlineData(AdminNotificationEventCatalog.CompanyWindDownRequested, PhysicalPolicy.AdministratorOnly)]
    [InlineData(AdminNotificationEventCatalog.CompanyWindDownRun, PhysicalPolicy.AdministratorOnly)]
    [InlineData(AdminNotificationEventCatalog.CompanyArchived, PhysicalPolicy.AdministratorOnly)]
    public void Every_Entry_Names_The_Administrator_Set_It_Is_Told_To(string key, string audience)
    {
        Assert.Equal(audience, AdminEventCatalog.Find(key).Audience);
    }

    [Fact]
    public void Every_Audience_Resolves_To_A_Set()
    {
        Assert.All(AdminEventCatalog.All, entry => Assert.NotEmpty(AdminRoleSets.For(entry.Audience)));
    }

    [Fact]
    public void Every_Arg_Name_Is_From_The_Closed_Vocabulary_And_Names_No_Person()
    {
        foreach (var entry in AdminEventCatalog.All)
        {
            Assert.NotEmpty(entry.EmailArgOrder);
            Assert.Equal(entry.EmailArgOrder.Count, entry.EmailArgOrder.Distinct(StringComparer.Ordinal).Count());
            foreach (var arg in entry.EmailArgOrder)
            {
                Assert.True(AllowedArgNames.Contains(arg), $"{entry.Key} carries '{arg}', which is not in the closed arg vocabulary.");
                Assert.False(IdentityOrFreeText.IsMatch(arg), $"{entry.Key} carries '{arg}', which names a person or free text.");
            }
        }

        Assert.All(AllowedArgNames, name => Assert.False(IdentityOrFreeText.IsMatch(name), $"'{name}' names a person or free text."));
    }

    [Fact]
    public void Every_Event_Deep_Links_From_An_Id_Or_Is_About_The_Company_Itself()
    {
        foreach (var entry in AdminEventCatalog.All)
        {
            var linksFromAnId = entry.EmailArgOrder.Any(a => a.EndsWith("Id", StringComparison.Ordinal));
            var aboutTheCompany = entry.Key.StartsWith("admin.company.", StringComparison.Ordinal);
            Assert.True(linksFromAnId || aboutTheCompany, $"{entry.Key} carries no id the console can deep-link from.");
        }
    }

    [Fact]
    public void Find_Answers_Each_Key_And_Refuses_Another()
    {
        foreach (var key in AdminNotificationEventCatalog.All)
        {
            Assert.Equal(key, AdminEventCatalog.Find(key).Key);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => AdminEventCatalog.Find(NotificationEventCatalog.OrderConfirmed));
    }
}
