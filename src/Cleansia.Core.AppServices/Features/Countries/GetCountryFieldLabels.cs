using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.Countries;

/// <summary>
/// What a country calls its business identifiers, and whether it demands them.
///
/// <para><b>This existed in the database and nowhere else.</b> <c>CountryConfiguration</c> has carried
/// <c>RegistrationNumberLabel</c>, <c>TaxIdLabel</c> and <c>VatNumberLabel</c> since it was seeded —
/// CZ and SK both set "IČO" — but no endpoint returned them, so every client hardcoded the Czech word
/// in its own translation files. A Ukrainian partner saw "IČO"; a Polish one would see it too, and
/// Poland has no IČO.</para>
///
/// <para><b>The client falls back to a neutral string when a label is null</b>, rather than the
/// platform inventing one. "Registration number" is correct everywhere and precise nowhere, which is
/// exactly what a fallback should be — the country's own word is better whenever we have it, and
/// flattening every country to the generic term to fix Poland would have cost CZ and SK the term
/// their own registries use.</para>
/// </summary>
public class GetCountryFieldLabels
{
    public record Request(string CountryId) : IRequest<CountryFieldLabelsDto?>;

    /// <param name="RegistrationNumberRequired">
    /// Whether the country demands one at all. Carried alongside the label because the client needs
    /// both to render the field: a country that does not require a registration number should not
    /// show it as mandatory just because the label resolved.
    /// </param>
    public record CountryFieldLabelsDto(
        string CountryId,
        string? RegistrationNumberLabel,
        string? RegistrationNumberFormat,
        bool RegistrationNumberRequired,
        string? TaxIdLabel,
        string? TaxIdFormat,
        string? VatNumberLabel,
        string? VatNumberFormat,
        bool VatNumberRequired);

    public class Handler(ICountryConfigurationRepository repository)
        : IRequestHandler<Request, CountryFieldLabelsDto?>
    {
        public async Task<CountryFieldLabelsDto?> Handle(Request request, CancellationToken cancellationToken)
        {
            var configuration = await repository.GetByCountryIdAsync(request.CountryId, cancellationToken);

            // Null, not an empty shell. A country with no configuration is one we know nothing about,
            // and a client told "no label, not required" would render a business-identity field as
            // optional for a jurisdiction that in fact demands one.
            if (configuration is null)
            {
                return null;
            }

            return new CountryFieldLabelsDto(
                CountryId: configuration.CountryId,
                RegistrationNumberLabel: configuration.RegistrationNumberLabel,
                RegistrationNumberFormat: configuration.RegistrationNumberFormat,
                RegistrationNumberRequired: configuration.RegistrationNumberRequired,
                TaxIdLabel: configuration.TaxIdLabel,
                TaxIdFormat: configuration.TaxIdFormat,
                VatNumberLabel: configuration.VatNumberLabel,
                VatNumberFormat: configuration.VatNumberFormat,
                VatNumberRequired: configuration.VatNumberRequired);
        }
    }
}
