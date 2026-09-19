using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.TenantSettings;

namespace Cleansia.Tests.Features.TenantSettings;

/// <summary>
/// The catalogue is the whole contract of what an operating company may configure: a key outside it
/// is refused, a value outside its range is refused, and a missing or unusable row resolves to the
/// default the sweeps were written against. The nine retention windows are its first category; each
/// entry's default is the <c>RetentionDefaults</c> constant, so the two can never disagree. The
/// lifecycle category holds the archive's chargeback horizon (ADR-0064 D3); the notifications
/// category holds the shared mailbox admin events are e-mailed to (ADR-0065 D3), an address whose
/// empty default means every administrator.
/// </summary>
public sealed class TenantSettingCatalogTests
{
    [Theory]
    [InlineData("retention.expired_codes.enabled", "true")]
    [InlineData("retention.stale_devices.days", "90")]
    [InlineData("retention.gdpr_requests.years", "3")]
    [InlineData("retention.order_pii.years", "2")]
    [InlineData("retention.withdrawn_consents.years", "3")]
    [InlineData("retention.deleted_documents.days", "365")]
    [InlineData("retention.notifications.days", "90")]
    [InlineData("retention.customer_audit.years", "3")]
    [InlineData("retention.dispute_text.years", "3")]
    public void Every_Retention_Window_Is_Catalogued_Under_Its_Contracted_Key_With_The_Sweeps_Default(
        string key, string expectedDefault)
    {
        var definition = TenantSettingCatalog.Find(key);

        Assert.NotNull(definition);
        Assert.Equal(key, definition.Key);
        Assert.Equal(expectedDefault, definition.DefaultValue);
        Assert.Equal(TenantSettingCatalog.RetentionCategory, definition.Category);
    }

    [Fact]
    public void The_Named_Entries_Are_The_Catalogued_Ones_Under_The_RetentionDefaults_Keys()
    {
        Assert.Same(TenantSettingCatalog.ExpiredCodesEnabled, TenantSettingCatalog.Find(RetentionDefaults.ExpiredCodesEnabledKey));
        Assert.Same(TenantSettingCatalog.StaleDevicesDays, TenantSettingCatalog.Find(RetentionDefaults.StaleDevicesDaysKey));
        Assert.Same(TenantSettingCatalog.GdprRequestsYears, TenantSettingCatalog.Find(RetentionDefaults.GdprRequestsYearsKey));
        Assert.Same(TenantSettingCatalog.OrderPiiYears, TenantSettingCatalog.Find(RetentionDefaults.OrderPiiYearsKey));
        Assert.Same(TenantSettingCatalog.WithdrawnConsentsYears, TenantSettingCatalog.Find(RetentionDefaults.WithdrawnConsentsYearsKey));
        Assert.Same(TenantSettingCatalog.DeletedDocumentsDays, TenantSettingCatalog.Find(RetentionDefaults.DeletedDocumentsDaysKey));
        Assert.Same(TenantSettingCatalog.NotificationsDays, TenantSettingCatalog.Find(RetentionDefaults.NotificationsDaysKey));
        Assert.Same(TenantSettingCatalog.CustomerAuditRetentionYears, TenantSettingCatalog.Find(RetentionDefaults.CustomerAuditRetentionYearsKey));
        Assert.Same(TenantSettingCatalog.DisputeTextRetentionYears, TenantSettingCatalog.Find(RetentionDefaults.DisputeTextRetentionYearsKey));

        Assert.Equal(RetentionDefaults.DefaultExpiredCodesEnabled, TenantSettingCatalog.ExpiredCodesEnabled.Default);
        Assert.Equal(RetentionDefaults.DefaultStaleDevicesDays, TenantSettingCatalog.StaleDevicesDays.Default);
        Assert.Equal(RetentionDefaults.DefaultGdprRequestsYears, TenantSettingCatalog.GdprRequestsYears.Default);
        Assert.Equal(RetentionDefaults.DefaultOrderPiiYears, TenantSettingCatalog.OrderPiiYears.Default);
        Assert.Equal(RetentionDefaults.DefaultWithdrawnConsentsYears, TenantSettingCatalog.WithdrawnConsentsYears.Default);
        Assert.Equal(RetentionDefaults.DefaultDeletedDocumentsDays, TenantSettingCatalog.DeletedDocumentsDays.Default);
        Assert.Equal(RetentionDefaults.DefaultNotificationsDays, TenantSettingCatalog.NotificationsDays.Default);
        Assert.Equal(RetentionDefaults.DefaultCustomerAuditRetentionYears, TenantSettingCatalog.CustomerAuditRetentionYears.Default);
        Assert.Equal(RetentionDefaults.DefaultDisputeTextRetentionYears, TenantSettingCatalog.DisputeTextRetentionYears.Default);
    }

    [Fact]
    public void The_Catalogue_Holds_Exactly_The_Nine_Retention_Keys_The_Lifecycle_Horizon_The_Admin_Mailbox_And_No_Duplicate()
    {
        Assert.Equal(11, TenantSettingCatalog.All.Count);
        Assert.Equal(TenantSettingCatalog.All.Count, TenantSettingCatalog.All.Select(d => d.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(9, TenantSettingCatalog.All.Count(d => d.Category == TenantSettingCatalog.RetentionCategory));
        Assert.Equal(1, TenantSettingCatalog.All.Count(d => d.Category == TenantSettingCatalog.LifecycleCategory));
        Assert.Equal(1, TenantSettingCatalog.All.Count(d => d.Category == TenantSettingCatalog.NotificationsCategory));
    }

    [Fact]
    public void The_Admin_Mailbox_Is_Catalogued_Under_Notifications_As_An_Email_With_An_Empty_Default_And_No_Range()
    {
        var mailbox = TenantSettingCatalog.Find("notifications.admin_email");

        Assert.Same(TenantSettingCatalog.AdminNotificationEmail, mailbox);
        Assert.Equal(TenantSettingCatalog.NotificationsCategory, mailbox!.Category);
        Assert.Equal(TenantSettingValueType.Email, mailbox.ValueType);
        Assert.Equal(string.Empty, mailbox.DefaultValue);
        Assert.Null(mailbox.Min);
        Assert.Null(mailbox.Max);
        Assert.Equal(string.Empty, TenantSettingCatalog.AdminNotificationEmail.Resolve(null));
    }

    [Theory]
    [InlineData("ops@example.com", "ops@example.com")]
    [InlineData("  Ops@Example.COM  ", "ops@example.com")]
    [InlineData("ops+admin@example.com", "ops+admin@example.com")]
    [InlineData("a@b", "a@b")]
    public void The_Admin_Mailbox_Accepts_An_Address_And_Stores_It_Trimmed_And_Lower_Cased(string raw, string canonical)
    {
        var mailbox = TenantSettingCatalog.AdminNotificationEmail;

        Assert.True(mailbox.IsValid(raw));
        Assert.Equal(canonical, mailbox.Canonicalize(raw));
        Assert.Equal(canonical, mailbox.Resolve(raw));
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("ops@")]
    [InlineData("@example.com")]
    [InlineData("ops@@example.com")]
    [InlineData("ops example@example.com")]
    [InlineData("Ops <ops@example.com>")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void The_Admin_Mailbox_Refuses_Anything_But_One_Address_And_Resolves_It_To_Every_Administrator(string? raw)
    {
        var mailbox = TenantSettingCatalog.AdminNotificationEmail;

        Assert.False(mailbox.IsValid(raw));
        Assert.Null(mailbox.Canonicalize(raw));
        Assert.Equal(string.Empty, mailbox.Resolve(raw));
    }

    [Fact]
    public void The_Admin_Mailbox_Refuses_An_Address_Longer_Than_The_Column_Allows()
    {
        var local = new string('a', EmailTenantSetting.MaxLength - "@example.com".Length + 1);

        Assert.False(TenantSettingCatalog.AdminNotificationEmail.IsValid($"{local}@example.com"));
        Assert.True(TenantSettingCatalog.AdminNotificationEmail.IsValid($"{local[1..]}@example.com"));
    }

    [Fact]
    public void The_Chargeback_Horizon_Is_Catalogued_Under_Lifecycle_With_180_Days_And_A_Zero_To_730_Range()
    {
        var horizon = TenantSettingCatalog.Find("lifecycle.chargeback_horizon_days");

        Assert.Same(TenantSettingCatalog.ChargebackHorizonDays, horizon);
        Assert.Equal(TenantSettingCatalog.LifecycleCategory, horizon!.Category);
        Assert.Equal("180", horizon.DefaultValue);
        Assert.Equal(0, horizon.Min);
        Assert.Equal(730, horizon.Max);
        Assert.Equal(TenantSettingValueType.Int, horizon.ValueType);
        Assert.True(TenantSettingCatalog.ChargebackHorizonDays.IsValid("0"));
        Assert.True(TenantSettingCatalog.ChargebackHorizonDays.IsValid("730"));
        Assert.False(TenantSettingCatalog.ChargebackHorizonDays.IsValid("731"));
        Assert.False(TenantSettingCatalog.ChargebackHorizonDays.IsValid("-1"));
        Assert.Equal(180, TenantSettingCatalog.ChargebackHorizonDays.Resolve(null));
    }

    [Theory]
    [InlineData("retention.customer_audit.year")]
    [InlineData("Retention.Customer_Audit.Years")]
    [InlineData("")]
    [InlineData(null)]
    public void A_Key_Outside_The_Catalogue_Is_Unknown(string? key)
    {
        Assert.Null(TenantSettingCatalog.Find(key));
    }

    [Fact]
    public void Every_Retention_Window_Has_A_Floor_Of_One_And_A_Ceiling_The_Date_Arithmetic_Can_Carry()
    {
        var windows = TenantSettingCatalog.All.OfType<IntTenantSetting>()
            .Where(w => w.Category == TenantSettingCatalog.RetentionCategory)
            .ToList();

        Assert.Equal(8, windows.Count);
        Assert.All(windows, w => Assert.Equal(1, w.Min));
        Assert.All(windows.Where(w => w.Key.EndsWith(".years", StringComparison.Ordinal)), w => Assert.Equal(100, w.Max));
        Assert.All(windows.Where(w => w.Key.EndsWith(".days", StringComparison.Ordinal)), w => Assert.Equal(36_500, w.Max));
    }

    [Theory]
    [InlineData("1", "1")]
    [InlineData(" 7 ", "7")]
    [InlineData("007", "7")]
    [InlineData("100", "100")]
    public void An_Int_Window_Accepts_A_Value_In_Range_And_Stores_Its_Canonical_Form(string raw, string canonical)
    {
        var window = TenantSettingCatalog.CustomerAuditRetentionYears;

        Assert.True(window.IsValid(raw));
        Assert.Equal(canonical, window.Canonicalize(raw));
        Assert.Equal(int.Parse(canonical), window.Resolve(raw));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("101")]
    [InlineData("3.5")]
    [InlineData("three")]
    [InlineData("")]
    [InlineData(null)]
    public void An_Int_Window_Refuses_A_Value_Outside_Its_Range_And_Resolves_It_To_The_Default(string? raw)
    {
        var window = TenantSettingCatalog.CustomerAuditRetentionYears;

        Assert.False(window.IsValid(raw));
        Assert.Null(window.Canonicalize(raw));
        Assert.Equal(RetentionDefaults.DefaultCustomerAuditRetentionYears, window.Resolve(raw));
    }

    [Theory]
    [InlineData("true", "true", true)]
    [InlineData("FALSE", "false", false)]
    [InlineData(" True ", "true", true)]
    public void A_Switch_Accepts_Either_Spelling_And_Stores_The_Lower_Case_Form(string raw, string canonical, bool value)
    {
        var setting = TenantSettingCatalog.ExpiredCodesEnabled;

        Assert.True(setting.IsValid(raw));
        Assert.Equal(canonical, setting.Canonicalize(raw));
        Assert.Equal(value, setting.Resolve(raw));
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public void A_Switch_Refuses_Anything_But_A_Boolean_And_Resolves_It_To_The_Default(string? raw)
    {
        var setting = TenantSettingCatalog.ExpiredCodesEnabled;

        Assert.False(setting.IsValid(raw));
        Assert.Null(setting.Canonicalize(raw));
        Assert.Equal(RetentionDefaults.DefaultExpiredCodesEnabled, setting.Resolve(raw));
    }

    [Fact]
    public void The_Value_Type_And_Range_Are_What_A_Typed_Editor_Reads()
    {
        Assert.Equal(TenantSettingValueType.Int, TenantSettingCatalog.StaleDevicesDays.ValueType);
        Assert.Equal(TenantSettingValueType.Bool, TenantSettingCatalog.ExpiredCodesEnabled.ValueType);
        Assert.Null(TenantSettingCatalog.ExpiredCodesEnabled.Min);
        Assert.Null(TenantSettingCatalog.ExpiredCodesEnabled.Max);
    }
}
