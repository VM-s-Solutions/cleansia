using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.TenantSettings;

/// <summary>
/// Reset removes the company's row so the catalogue default applies again. A hard delete, not a
/// deactivation: the row is a value override with no history of its own, and the unique
/// <c>(TenantId, Key)</c> index would refuse the next Set if a deactivated row stayed behind. Idempotent
/// — resetting a key that holds no row is a success that changes nothing.
/// </summary>
public sealed class ResetTenantSettingTests
{
    private const string Key = RetentionDefaults.CustomerAuditRetentionYearsKey;

    private readonly Mock<ITenantConfigurationRepository> _repository = new();
    private readonly Mock<IAuditContext> _auditContext = new();

    private ResetTenantSetting.Handler CreateHandler() => new(_repository.Object, _auditContext.Object);

    private static ResetTenantSetting.Validator Validator() => new();

    private TenantConfiguration? ArrangeRow(string key, string? value)
    {
        var row = value is null ? null : TenantConfiguration.Create(key, value);
        _repository.Setup(r => r.GetByKeyAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync(row);
        return row;
    }

    [Fact]
    public async Task An_Empty_Key_Is_Required()
    {
        var result = await Validator().ValidateAsync(new ResetTenantSetting.Command(""));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.Required, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Key_Outside_The_Catalogue_Is_Refused()
    {
        var result = await Validator().ValidateAsync(new ResetTenantSetting.Command("retention.unknown.years"));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.TenantSettingUnknownKey, error.ErrorMessage);
        Assert.Equal(nameof(ResetTenantSetting.Command.Key), error.PropertyName);
    }

    [Fact]
    public async Task A_Catalogued_Key_Passes()
    {
        var result = await Validator().ValidateAsync(new ResetTenantSetting.Command(Key));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Row_Is_Removed_And_The_Default_Is_Reported_Back()
    {
        var row = ArrangeRow(Key, "1");

        var result = await CreateHandler().Handle(new ResetTenantSetting.Command(Key), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Key, result.Value.Key);
        Assert.Equal(RetentionDefaults.DefaultCustomerAuditRetentionYears.ToString(), result.Value.Value);
        _repository.Verify(r => r.Remove(row!), Times.Once);
        _repository.Verify(r => r.Deactivate(It.IsAny<TenantConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task A_Key_With_No_Row_Is_Reset_Idempotently()
    {
        ArrangeRow(Key, null);

        var result = await CreateHandler().Handle(new ResetTenantSetting.Command(Key), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RetentionDefaults.DefaultCustomerAuditRetentionYears.ToString(), result.Value.Value);
        _repository.Verify(r => r.Remove(It.IsAny<TenantConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task The_Audit_Snapshot_Carries_The_Value_Before_And_Nothing_After()
    {
        ArrangeRow(Key, "1");
        TenantSettingSnapshot? before = null;
        TenantSettingSnapshot? after = null;
        _auditContext
            .Setup(a => a.RecordChange("TenantSetting", Key, It.IsAny<object>(), It.IsAny<object>(), null))
            .Callback<string, string, object, object, string?>((_, _, b, a, _) =>
            {
                before = (TenantSettingSnapshot)b;
                after = (TenantSettingSnapshot)a;
            });

        await CreateHandler().Handle(new ResetTenantSetting.Command(Key), CancellationToken.None);

        Assert.Equal(new TenantSettingSnapshot(Key, "1"), before);
        Assert.Equal(new TenantSettingSnapshot(Key, null), after);
    }

    [Fact]
    public async Task The_Handler_Guards_The_Catalogue_Rather_Than_Trusting_The_Validator()
    {
        var result = await CreateHandler().Handle(new ResetTenantSetting.Command("retention.unknown.years"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.TenantSettingUnknownKey, result.Error!.Message);
        _repository.Verify(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
