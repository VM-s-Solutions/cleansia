using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.PropertySizes;

/// <summary>
/// The property sizes a market offers, labelled in the caller's language.
/// </summary>
/// <remarks>
/// <para><b>The label is the only country-specific part.</b> An order stores
/// <c>Rooms</c> and <c>Bathrooms</c> as plain integers and
/// <c>OrderPricingCalculator</c> prices on those, so nothing anywhere persists
/// "3+kk" — it is a Czech presentation string and nothing more. A new market is
/// a new set of rows over the same two numbers, which is why expansion needed no
/// domain change at all. → /decisions/adr-0056</para>
///
/// <para><b>An unknown or unseeded country returns an EMPTY list, not an error.</b>
/// The caller is a price calculator on a public page: a market we have no presets
/// for is a market we do not serve yet, and the honest render is "no sizes to
/// choose", not a 500.</para>
/// </remarks>
public class GetPropertySizePresets
{
    /// <param name="IsoCode">Three-letter country code — the public site holds a code, not a ULID.</param>
    /// <param name="LanguageCode">Which label to resolve; falls back to English, then to the code.</param>
    public record Request(string IsoCode, string LanguageCode) : IRequest<IReadOnlyList<PropertySizePresetDto>>;

    /// <param name="Code">Stable identifier, never shown to a user.</param>
    /// <param name="Label">Already resolved for the requested language.</param>
    public record PropertySizePresetDto(
        string Code,
        string Label,
        int SortOrder,
        int Rooms,
        int Bathrooms);

    public class Handler(IPropertySizePresetRepository repository)
        : IRequestHandler<Request, IReadOnlyList<PropertySizePresetDto>>
    {
        private const string FallbackLanguage = "en";

        public async Task<IReadOnlyList<PropertySizePresetDto>> Handle(
            Request request,
            CancellationToken cancellationToken)
        {
            var presets = await repository.GetForCountryIsoCodeAsync(request.IsoCode, cancellationToken);

            return presets
                .Select(preset => new PropertySizePresetDto(
                    Code: preset.Code,
                    Label: ResolveLabel(preset, request.LanguageCode),
                    SortOrder: preset.SortOrder,
                    Rooms: preset.Rooms,
                    Bathrooms: preset.Bathrooms))
                .ToList();
        }

        /// <summary>
        /// Requested language, then English, then the code itself.
        /// </summary>
        /// <remarks>
        /// The code is a deliberate last resort rather than an empty string: a
        /// visitor seeing "CZ_3KK" can still tell the options apart and pick one,
        /// and it is obvious to us that a translation is missing. A blank chip is
        /// neither.
        /// </remarks>
        private static string ResolveLabel(
            Cleansia.Core.Domain.Configuration.PropertySizePreset preset,
            string languageCode)
        {
            if (preset.Translations.TryGetValue(languageCode, out var requested) &&
                !string.IsNullOrWhiteSpace(requested.Name))
            {
                return requested.Name;
            }

            if (preset.Translations.TryGetValue(FallbackLanguage, out var english) &&
                !string.IsNullOrWhiteSpace(english.Name))
            {
                return english.Name;
            }

            return preset.Code;
        }
    }
}
