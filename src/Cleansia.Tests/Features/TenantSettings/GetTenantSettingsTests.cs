using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.TenantSettings;

/// <summary>
/// The admin read: every catalogue key, always — the page is the catalogue, not the table — with the
/// default, the value in effect and whether the company holds a row for it. A row the catalogue would
/// not accept today is reported as an override whose effect is the default, which is exactly what the
/// sweeps do with it.
/// </summary>
public sealed class GetTenantSettingsTests
{
    private readonly Mock<ITenantConfigurationRepository> _repository = new();

    private GetTenantSettings.Handler CreateHandler() => new(_repository.Object);

    private void ArrangeRows(params (string Key, string Value)[] rows) =>
        _repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows.Select(r => TenantConfiguration.Create(r.Key, r.Value)).ToList());

    [Fact]
    public async Task Every_Catalogue_Key_Is_Listed_At_Its_Default_When_The_Company_Holds_No_Row()
    {
        ArrangeRows();

        var result = await CreateHandler().Handle(new GetTenantSettings.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantSettingCatalog.All.Select(d => d.Key), result.Value.Settings.Select(s => s.Key));
        Assert.All(result.Value.Settings, s =>
        {
            Assert.False(s.IsOverridden);
            Assert.Equal(s.DefaultValue, s.EffectiveValue);
        });
    }

    [Fact]
    public async Task A_Row_Is_Reported_As_The_Value_In_Effect()
    {
        ArrangeRows((RetentionDefaults.CustomerAuditRetentionYearsKey, "1"));

        var result = await CreateHandler().Handle(new GetTenantSettings.Query(), CancellationToken.None);

        var window = Assert.Single(result.Value.Settings, s => s.Key == RetentionDefaults.CustomerAuditRetentionYearsKey);
        Assert.True(window.IsOverridden);
        Assert.Equal("1", window.EffectiveValue);
        Assert.Equal("3", window.DefaultValue);
        Assert.Equal(TenantSettingValueType.Int, window.ValueType);
        Assert.Equal(1, window.Min);
        Assert.Equal(100, window.Max);
        Assert.Equal(TenantSettingCatalog.RetentionCategory, window.Category);
        Assert.All(result.Value.Settings.Where(s => s.Key != window.Key), s => Assert.False(s.IsOverridden));
    }

    [Fact]
    public async Task A_Row_The_Catalogue_Would_Refuse_Today_Is_An_Override_Whose_Effect_Is_The_Default()
    {
        ArrangeRows((RetentionDefaults.CustomerAuditRetentionYearsKey, "0"));

        var result = await CreateHandler().Handle(new GetTenantSettings.Query(), CancellationToken.None);

        var window = Assert.Single(result.Value.Settings, s => s.Key == RetentionDefaults.CustomerAuditRetentionYearsKey);
        Assert.True(window.IsOverridden);
        Assert.Equal(window.DefaultValue, window.EffectiveValue);
    }

    [Fact]
    public async Task A_Row_Under_A_Key_The_Catalogue_No_Longer_Knows_Is_Not_Listed()
    {
        ArrangeRows(("retention.retired.days", "5"));

        var result = await CreateHandler().Handle(new GetTenantSettings.Query(), CancellationToken.None);

        Assert.DoesNotContain(result.Value.Settings, s => s.Key == "retention.retired.days");
        Assert.Equal(TenantSettingCatalog.All.Count, result.Value.Settings.Count);
    }
}
