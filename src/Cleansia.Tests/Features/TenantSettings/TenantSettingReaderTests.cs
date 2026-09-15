using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Domain.Configuration;
using Moq;

namespace Cleansia.Tests.Features.TenantSettings;

/// <summary>
/// The typed read every sweep and the erasure make: the ambient company's row when it holds a value
/// the catalogue accepts, the catalogue default otherwise — no row, a row outside the range, a row that
/// is not a number at all. The floor the sweeps used to enforce by hand lives in the catalogue now.
/// </summary>
public sealed class TenantSettingReaderTests
{
    private readonly Mock<IAppConfigurationProvider> _provider = new();

    private void Stored(string key, string? value) =>
        _provider.Setup(p => p.GetTenantSettingAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync(value);

    [Fact]
    public async Task No_Row_Resolves_To_The_Catalogue_Default()
    {
        Stored(RetentionDefaults.CustomerAuditRetentionYearsKey, null);

        var years = await _provider.Object.GetAsync(TenantSettingCatalog.CustomerAuditRetentionYears, CancellationToken.None);

        Assert.Equal(RetentionDefaults.DefaultCustomerAuditRetentionYears, years);
    }

    [Fact]
    public async Task A_Row_In_Range_Resolves_To_Its_Value()
    {
        Stored(RetentionDefaults.CustomerAuditRetentionYearsKey, "1");

        var years = await _provider.Object.GetAsync(TenantSettingCatalog.CustomerAuditRetentionYears, CancellationToken.None);

        Assert.Equal(1, years);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("101")]
    [InlineData("soon")]
    public async Task A_Row_Outside_The_Range_Resolves_To_The_Default(string stored)
    {
        Stored(RetentionDefaults.CustomerAuditRetentionYearsKey, stored);

        var years = await _provider.Object.GetAsync(TenantSettingCatalog.CustomerAuditRetentionYears, CancellationToken.None);

        Assert.Equal(RetentionDefaults.DefaultCustomerAuditRetentionYears, years);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("true", true)]
    [InlineData(null, true)]
    [InlineData("off", true)]
    public async Task A_Switch_Reads_Either_Spelling_And_Falls_Back_To_On(string? stored, bool expected)
    {
        Stored(RetentionDefaults.ExpiredCodesEnabledKey, stored);

        var enabled = await _provider.Object.GetAsync(TenantSettingCatalog.ExpiredCodesEnabled, CancellationToken.None);

        Assert.Equal(expected, enabled);
    }

    [Fact]
    public async Task The_Read_Is_Keyed_On_The_Definitions_Key()
    {
        Stored(RetentionDefaults.NotificationsDaysKey, "30");
        Stored(RetentionDefaults.StaleDevicesDaysKey, "10");

        Assert.Equal(30, await _provider.Object.GetAsync(TenantSettingCatalog.NotificationsDays, CancellationToken.None));
        Assert.Equal(10, await _provider.Object.GetAsync(TenantSettingCatalog.StaleDevicesDays, CancellationToken.None));
    }
}
