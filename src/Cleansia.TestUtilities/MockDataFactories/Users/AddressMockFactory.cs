using Cleansia.Core.Domain.Users;

namespace Cleansia.TestUtilities.MockDataFactories.Users;

public class AddressMockFactory
{
    public class AddressPartial
    {
        public string? Street { get; set; }

        public string? City { get; set; }

        public string? ZipCode { get; set; }

        public string? CountryId { get; set; }

        public string? State { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }

    public static Address Generate(AddressPartial? mergeFrom = null)
    {
        var partial = mergeFrom ?? new AddressPartial();
        var address = Address.Create(
            street: partial.Street ?? "123 Main Street",
            city: partial.City ?? "Prague",
            zipCode: partial.ZipCode ?? "11000",
            countryId: partial.CountryId ?? "cz",
            state: partial.State,
            latitude: partial.Latitude,
            longitude: partial.Longitude);
        address.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        return address;
    }
}
