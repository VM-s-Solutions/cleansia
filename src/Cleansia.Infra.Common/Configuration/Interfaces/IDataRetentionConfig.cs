namespace Cleansia.Infra.Common.Configuration.Interfaces;

/// <summary>
/// The master switch for the GDPR data-retention sweep (section <c>DataRetention</c>). The sweep deletes or
/// anonymises personal data the platform may no longer keep — expired user codes, stale devices, old GDPR
/// requests, customer PII on completed orders, withdrawn consents, superseded documents and notifications —
/// so it is a compliance clock rather than a housekeeping preference.
///
/// <para><b>Absence must mean ON.</b> This switch previously lived in the <c>FeatureFlags</c> table, read
/// through <c>IAppConfigurationProvider.IsFeatureEnabledAsync</c>, which resolves a missing row to
/// <c>false</c>. No migration ever inserted the row and the only INSERT lives in a development-only seed
/// fixture, so on every deployed database the sweep decided it was switched off and reported success
/// without deleting anything. Binding from configuration keeps the default in the code, where an empty
/// database cannot silence it, and makes "off" something somebody had to type.</para>
/// </summary>
public interface IDataRetentionConfig
{
    /// <summary>
    /// Master switch. When false the sweep is a no-op, so it can be disabled per environment. Defaults to
    /// true when the configuration section is absent — see the type remarks for why that direction matters.
    /// </summary>
    bool Enabled { get; set; }
}
