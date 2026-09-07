using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Configuration;

/// <summary>
/// One selectable property size for a market — "3+kk" in Czechia, "3-Zimmer" in
/// Germany, "2-bed" in the UK — mapped onto the two integers the platform
/// actually prices on.
/// </summary>
/// <remarks>
/// The label is the only country-specific part. <see cref="Orders.Order.Rooms"/>
/// and <see cref="Orders.Order.Bathrooms"/> are plain integers and
/// <c>OrderPricingCalculator</c> computes
/// <c>BasePrice + PerRoomPrice * (rooms + bathrooms)</c>, so nothing persists
/// "3+kk" and a new market is a new preset list over the same two numbers.
///
/// Because an order stores the integers rather than a preset id, deactivating or
/// relabelling a preset can never make a historic order unpriceable. That is what
/// makes this catalogue safe to edit.
///
/// Catalogue data, not tenant data: deliberately <b>not</b> <c>ITenantEntity</c>,
/// for the same reason <see cref="Country"/> and <c>ServiceCity</c> are not.
/// → /decisions/adr-0056
/// </remarks>
public class PropertySizePreset : Auditable
{
    private IDictionary<string, Translation> _translations = new Dictionary<string, Translation>();

    private PropertySizePreset() { }

    /// <summary>The market this preset belongs to.</summary>
    public string CountryId { get; private set; } = default!;

    public Country? Country { get; private set; }

    /// <summary>
    /// Stable identifier such as <c>CZ_3KK</c>. Never shown to a user — the label
    /// comes from <see cref="Translations"/> — so it can outlive a rename.
    /// </summary>
    public string Code { get; private set; } = default!;

    /// <summary>
    /// Display order. Explicit because the natural order is neither alphabetical
    /// nor by room count once a market mixes flats and houses.
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>Rooms this preset stands for — an input to pricing.</summary>
    public int Rooms { get; private set; }

    /// <summary>Bathrooms this preset stands for — an input to pricing.</summary>
    public int Bathrooms { get; private set; }

    /// <summary>Per-language label, same owned-dictionary shape as the other catalogues.</summary>
    public IReadOnlyDictionary<string, Translation> Translations => _translations.AsReadOnly();

    public static PropertySizePreset Create(
        string countryId,
        string code,
        int sortOrder,
        int rooms,
        int bathrooms)
    {
        if (rooms < 0 || bathrooms < 0)
        {
            throw new ArgumentException("A property size cannot have a negative room count.");
        }

        return new PropertySizePreset
        {
            CountryId = countryId,
            Code = code,
            SortOrder = sortOrder,
            Rooms = rooms,
            Bathrooms = bathrooms,
        };
    }

    public PropertySizePreset SetTranslation(string languageCode, string name)
    {
        _translations[languageCode] = new Translation { Name = name, Description = string.Empty };
        return this;
    }

    public PropertySizePreset UpdateSize(int rooms, int bathrooms)
    {
        if (rooms < 0 || bathrooms < 0)
        {
            throw new ArgumentException("A property size cannot have a negative room count.");
        }

        Rooms = rooms;
        Bathrooms = bathrooms;
        return this;
    }

    public PropertySizePreset Reorder(int sortOrder)
    {
        SortOrder = sortOrder;
        return this;
    }
}
