using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.Clients.Abstractions;
using Microsoft.Extensions.Logging;
using Polly;

namespace Cleansia.Infra.Services.BusinessRegistry;

/// <summary>
/// The Czech register of economic subjects (ARES), asked for a company ID (IČO). Nothing it answers is
/// stored.
/// </summary>
public sealed class AresBusinessRegistry(
    IHttpClientFactory httpClientFactory,
    AresConfig config,
    ILogger<AresBusinessRegistry> logger) : IBusinessRegistry
{
    public const string HttpClientName = "Ares";
    public const string Endpoint = "https://ares.gov.cz/ekonomicke-subjekty-v-be/rest/ekonomicke-subjekty/";

    // ARES marks each source register it holds the subject in; the trade register (RŽP) reads AKTIVNI
    // while at least one trade licence is in force.
    private const string ActiveSource = "AKTIVNI";

    private static readonly string[] CountryIsoCodes = ["CZE", "CZ"];

    public static readonly EventId UnavailableEvent = new(7186_01, "AresLookupUnavailable");

    public async Task<BusinessRegistryRecord> LookupAsync(
        string countryIsoCode,
        string registrationNumber,
        CancellationToken cancellationToken)
    {
        if (!config.Enabled || !CountryIsoCodes.Contains(countryIsoCode, StringComparer.OrdinalIgnoreCase))
        {
            return BusinessRegistryRecord.NotConsulted;
        }

        var ico = registrationNumber.Trim();
        if (ico.Length != 8 || !ico.All(char.IsAsciiDigit))
        {
            return BusinessRegistryRecord.NotRegistered;
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(Endpoint + ico, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return BusinessRegistryRecord.NotRegistered;
            }

            if (!response.IsSuccessStatusCode)
            {
                return Unavailable(IntegrationFailureClassifier.FromHttpStatus((int)response.StatusCode), exception: null);
            }

            var subject = await response.Content.ReadFromJsonAsync<AresSubject>(cancellationToken);
            if (subject is null)
            {
                return Unavailable(IntegrationFailureClass.Permanent, exception: null);
            }

            return new BusinessRegistryRecord(
                BusinessRegistryAnswer.Registered,
                Ceased: subject.DatumZaniku is not null,
                TradeLicenceActive: subject.SeznamRegistraci?.StavZdrojeRzp == ActiveSource);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                   && ex is HttpRequestException or OperationCanceledException
                                       or ExecutionRejectedException or JsonException or NotSupportedException)
        {
            return Unavailable(
                ex is JsonException or NotSupportedException
                    ? IntegrationFailureClass.Permanent
                    : IntegrationFailureClassifier.FromException(ex),
                ex);
        }
    }

    private BusinessRegistryRecord Unavailable(IntegrationFailureClass failureClass, Exception? exception)
    {
        IntegrationFailureMetrics.Record(HttpClientName, failureClass);

        var level = failureClass is IntegrationFailureClass.AuthConfig or IntegrationFailureClass.Permanent
            ? LogLevel.Error
            : LogLevel.Warning;
        logger.Log(level, UnavailableEvent, exception, "ARES lookup unavailable: {FailureClass}.", failureClass);

        return BusinessRegistryRecord.Unavailable;
    }

    private sealed record AresSubject(string? DatumZaniku, AresRegistrations? SeznamRegistraci);

    private sealed record AresRegistrations(string? StavZdrojeRzp);
}
