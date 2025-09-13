using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Users;

public class Address : Auditable
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

    public string CountryId { get; private set; }
    public Country? Country { get; private set; }

    public static Address Create(string street, string city, string zipCode, string countryId) =>
        new()
        {
            Street = street,
            City = city,
            ZipCode = zipCode,
            CountryId = countryId
        };

    public Address Update(string street, string city, string zipCode, string countryId)
    {
        Street = street;
        City = city;
        ZipCode = zipCode;
        CountryId = countryId;

        return this;
    }
}