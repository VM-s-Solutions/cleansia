using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.ServiceAreas;

/// <summary>
/// A city the company actually serves within a serviced country. Customer
/// order creation must pick an address a row here serves (<see cref="CityNameMatch.Serves"/>).
/// Employee addresses do NOT have to match — cleaners can live anywhere
/// and commute into served cities.
///
/// <see cref="ZipPrefix"/>, when set, decides by the address's postcode: a row with one serves a
/// valid postcode that starts with it and refuses any other, whatever the city text says.
/// </summary>
public class ServiceCity : Auditable
{
    [Required]
    [MaxLength(26)]
    public string CountryId { get; private set; } = string.Empty;

    public Country Country { get; private set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; private set; } = string.Empty;

    [MaxLength(20)]
    public string? ZipPrefix { get; private set; }

    public static ServiceCity Create(string countryId, string name, string? zipPrefix = null) =>
        new()
        {
            CountryId = countryId,
            Name = name.Trim(),
            ZipPrefix = string.IsNullOrWhiteSpace(zipPrefix) ? null : zipPrefix.Trim(),
        };

    public ServiceCity Update(string name, string? zipPrefix)
    {
        Name = name.Trim();
        ZipPrefix = string.IsNullOrWhiteSpace(zipPrefix) ? null : zipPrefix.Trim();
        return this;
    }
}
