namespace Cleansia.Infra.Fiscal.Countries.Czechia;

/// <summary>
/// Configuration for Czech EET 2.0 fiscal integration (launching January 2027).
/// Bind from <c>appsettings.json</c> section <c>Fiscal:CzechEet2</c>.
/// </summary>
public class CzechEet2Options
{
    public const string SectionName = "Fiscal:CzechEet2";

    /// <summary>Base URL of the Czech Financial Administration fiscal API.</summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>API key or OAuth client ID issued to the taxpayer.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Path to the taxpayer's X.509 certificate for request signing.</summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>Password for the certificate file.</summary>
    public string CertificatePassword { get; set; } = string.Empty;

    /// <summary>Taxpayer identifier (DIČ).</summary>
    public string TaxpayerIdentifier { get; set; } = string.Empty;

    /// <summary>Unique business premise identifier assigned by the authority.</summary>
    public string BusinessPremiseId { get; set; } = string.Empty;

    /// <summary>Unique cash register identifier assigned by the taxpayer.</summary>
    public string CashRegisterId { get; set; } = string.Empty;

    /// <summary>
    /// Feature flag. Set to <c>true</c> only when EET 2.0 is live and the
    /// <see cref="CzechEet2FiscalService"/> implementation is complete.
    /// </summary>
    public bool Enabled { get; set; }
}
