using Cleansia.Core.Fiscal.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cleansia.Infra.Fiscal.Countries.Czechia;

/// <summary>
/// Stub implementation for Czech EET 2.0 fiscal integration.
/// Launches January 1, 2027. Implementation body will be filled in once the
/// Czech Financial Administration publishes the API specification.
///
/// Until then, this class is registered in DI only when <see cref="CzechEet2Options.Enabled"/>
/// is <c>true</c>, which is never the case in any current environment.
/// </summary>
public class CzechEet2FiscalService : IFiscalService
{
    private readonly CzechEet2Options _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CzechEet2FiscalService> _logger;

    public string ProviderKey => "cz-eet2";

    public string CountryCode => "CZ";

    public CzechEet2FiscalService(
        IOptions<CzechEet2Options> options,
        HttpClient httpClient,
        ILogger<CzechEet2FiscalService> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<FiscalResult> RegisterReceiptAsync(
        FiscalReceiptRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "CzechEet2FiscalService.RegisterReceiptAsync called but EET 2.0 implementation is not yet available. ReceiptNumber={ReceiptNumber}",
            request.ReceiptNumber);

        // Implementation roadmap once the API spec is published:
        //   1. Build the authority-specific payload from FiscalReceiptRequest
        //   2. Sign it with the cert (_options.CertificatePath + _options.CertificatePassword)
        //   3. POST to _options.ApiUrl with appropriate auth
        //   4. Parse the response → extract FIK code
        //   5. Return FiscalResult.Success(fik, registeredAt)
        return Task.FromResult(FiscalResult.ConfigurationError(
            errorCode: "NOT_IMPLEMENTED",
            errorMessage: "Czech EET 2.0 integration is not yet implemented."));
    }
}
