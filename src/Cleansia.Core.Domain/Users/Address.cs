using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Users;

public class Address : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(255)]
    public string Street { get; private set; }

    [Required]
    [MaxLength(100)]
    public string City { get; private set; }

    [Required]
    [MaxLength(20)]
    public string ZipCode { get; private set; }

    [MaxLength(100)]
    public string? State { get; private set; }

    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    public string CountryId { get; private set; }
    public Country? Country { get; private set; }

    public static Address Create(string street, string city, string zipCode, string countryId, string? state = null, double? latitude = null, double? longitude = null) =>
        new()
        {
            Street = street,
            City = city,
            ZipCode = zipCode,
            State = state,
            CountryId = countryId,
            Latitude = latitude,
            Longitude = longitude
        };

    public Address Update(string street, string city, string zipCode, string countryId, string? state = null, double? latitude = null, double? longitude = null)
    {
        Street = street;
        City = city;
        ZipCode = zipCode;
        State = state;
        CountryId = countryId;
        Latitude = latitude;
        Longitude = longitude;

        return this;
    }

    public Address Anonymize()
    {
        Street = AnonymizationMarker.Value;
        City = AnonymizationMarker.Value;
        ZipCode = AnonymizationMarker.Value;
        State = AnonymizationMarker.Value;
        Latitude = null;
        Longitude = null;
        return this;
    }
}