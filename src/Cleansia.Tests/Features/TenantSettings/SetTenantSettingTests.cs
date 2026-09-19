using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.TenantSettings;

/// <summary>
/// The admin writer: an upsert of one catalogued key for the admin's own company. The validator refuses
/// a key outside the catalogue and a value outside its range; the handler stores the canonical form,
/// creating the row or updating it, and records the value before and after for the audit trail.
/// </summary>
public sealed class SetTenantSettingTests
{
    private const string Key = RetentionDefaults.CustomerAuditRetentionYearsKey;

    private readonly Mock<ITenantConfigurationRepository> _repository = new();
    private readonly Mock<IAuditContext> _auditContext = new();

    private SetTenantSetting.Handler CreateHandler() => new(_repository.Object, _auditContext.Object);

    private static SetTenantSetting.Validator Validator() => new();

    private TenantConfiguration? ArrangeRow(string key, string? value)
    {
        var row = value is null ? null : TenantConfiguration.Create(key, value);
        _repository.Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync(row);
        return row;
    }

    [Fact]
    public async Task An_Empty_Key_Is_Required()
    {
        var result = await Validator().ValidateAsync(new SetTenantSetting.Command("", "1"));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal(nameof(SetTenantSetting.Command.Key), error.PropertyName);
    }

    [Fact]
    public async Task A_Key_Outside_The_Catalogue_Is_Refused_And_Its_Value_Is_Not_Judged()
    {
        var result = await Validator().ValidateAsync(new SetTenantSetting.Command("retention.unknown.years", "not-a-number"));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.TenantSettingUnknownKey, error.ErrorMessage);
        Assert.Equal(nameof(SetTenantSetting.Command.Key), error.PropertyName);
    }

    [Fact]
    public async Task An_Empty_Value_Is_Required()
    {
        var result = await Validator().ValidateAsync(new SetTenantSetting.Command(Key, ""));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal(nameof(SetTenantSetting.Command.Value), error.PropertyName);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("two")]
    public async Task A_Value_Outside_The_Keys_Range_Is_Refused(string value)
    {
        var result = await Validator().ValidateAsync(new SetTenantSetting.Command(Key, value));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.TenantSettingInvalidValue, error.ErrorMessage);
        Assert.Equal(nameof(SetTenantSetting.Command.Value), error.PropertyName);
    }

    [Fact]
    public async Task A_Switch_Refuses_A_Non_Boolean()
    {
        var result = await Validator().ValidateAsync(new SetTenantSetting.Command(RetentionDefaults.ExpiredCodesEnabledKey, "1"));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.TenantSettingInvalidValue, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("ops@")]
    [InlineData("Ops <ops@example.com>")]
    public async Task The_Admin_Mailbox_Refuses_What_Is_Not_One_Address(string value)
    {
        var result = await Validator().ValidateAsync(new SetTenantSetting.Command(TenantSettingCatalog.AdminNotificationEmailKey, value));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.TenantSettingInvalidValue, error.ErrorMessage);
        Assert.Equal(nameof(SetTenantSetting.Command.Value), error.PropertyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task The_Admin_Mailbox_Cannot_Be_Set_Empty_Because_Unset_Is_The_Reset(string value)
    {
        var result = await Validator().ValidateAsync(new SetTenantSetting.Command(TenantSettingCatalog.AdminNotificationEmailKey, value));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.Required, error.ErrorMessage);
        Assert.Equal(nameof(SetTenantSetting.Command.Value), error.PropertyName);
    }

    [Fact]
    public async Task The_Admin_Mailbox_Is_Stored_Trimmed_And_Lower_Cased_Under_The_Notifications_Category()
    {
        ArrangeRow(TenantSettingCatalog.AdminNotificationEmailKey, null);
        TenantConfiguration? added = null;
        _repository.Setup(r => r.Add(It.IsAny<TenantConfiguration>())).Callback<TenantConfiguration>(row => added = row);

        var result = await CreateHandler().Handle(
            new SetTenantSetting.Command(TenantSettingCatalog.AdminNotificationEmailKey, "  Ops@Example.com "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ops@example.com", result.Value.Value);
        Assert.NotNull(added);
        Assert.Equal("ops@example.com", added.Value);
        Assert.Equal(TenantSettingCatalog.NotificationsCategory, added.Category);
    }

    [Theory]
    [InlineData(Key, "1")]
    [InlineData(Key, " 100 ")]
    [InlineData(RetentionDefaults.ExpiredCodesEnabledKey, "False")]
    [InlineData(TenantSettingCatalog.AdminNotificationEmailKey, "ops@example.com")]
    public async Task A_Catalogued_Key_With_A_Value_In_Range_Passes(string key, string value)
    {
        var result = await Validator().ValidateAsync(new SetTenantSetting.Command(key, value));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Key_With_No_Row_Yet_Creates_One_In_The_Catalogue_Category_With_The_Canonical_Value()
    {
        ArrangeRow(Key, null);
        TenantConfiguration? added = null;
        _repository.Setup(r => r.Add(It.IsAny<TenantConfiguration>())).Callback<TenantConfiguration>(row => added = row);

        var result = await CreateHandler().Handle(new SetTenantSetting.Command(Key, " 007 "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Key, result.Value.Key);
        Assert.Equal("7", result.Value.Value);
        Assert.NotNull(added);
        Assert.Equal(Key, added.Key);
        Assert.Equal("7", added.Value);
        Assert.Equal(TenantSettingCatalog.RetentionCategory, added.Category);
    }

    [Fact]
    public async Task A_Key_With_A_Row_Updates_It_In_Place_And_Adds_Nothing()
    {
        var row = ArrangeRow(Key, "5");

        var result = await CreateHandler().Handle(new SetTenantSetting.Command(Key, "2"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("2", row!.Value);
        _repository.Verify(r => r.Add(It.IsAny<TenantConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task The_Audit_Snapshot_Carries_The_Value_Before_And_After_Keyed_On_The_Setting()
    {
        ArrangeRow(Key, "5");
        TenantSettingSnapshot? before = null;
        TenantSettingSnapshot? after = null;
        _auditContext
            .Setup(a => a.RecordChange("TenantSetting", Key, It.IsAny<object>(), It.IsAny<object>(), null))
            .Callback<string, string, object, object, string?>((_, _, b, a, _) =>
            {
                before = (TenantSettingSnapshot)b;
                after = (TenantSettingSnapshot)a;
            });

        await CreateHandler().Handle(new SetTenantSetting.Command(Key, "2"), CancellationToken.None);

        Assert.Equal(new TenantSettingSnapshot(Key, "5"), before);
        Assert.Equal(new TenantSettingSnapshot(Key, "2"), after);
    }

    [Fact]
    public async Task A_First_Write_Records_No_Value_Before()
    {
        ArrangeRow(Key, null);
        TenantSettingSnapshot? before = null;
        _auditContext
            .Setup(a => a.RecordChange("TenantSetting", Key, It.IsAny<object>(), It.IsAny<object>(), null))
            .Callback<string, string, object, object, string?>((_, _, b, _, _) => before = (TenantSettingSnapshot)b);

        await CreateHandler().Handle(new SetTenantSetting.Command(Key, "2"), CancellationToken.None);

        Assert.Equal(new TenantSettingSnapshot(Key, null), before);
    }
}
