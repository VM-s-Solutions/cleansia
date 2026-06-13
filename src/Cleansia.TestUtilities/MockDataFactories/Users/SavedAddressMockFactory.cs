using Cleansia.Core.Domain.Users;

namespace Cleansia.TestUtilities.MockDataFactories.Users;

public class SavedAddressMockFactory
{
    public class SavedAddressPartial
    {
        public string? Id { get; set; }

        public string? UserId { get; set; }

        public string? AddressId { get; set; }

        public string? Label { get; set; }

        public bool IsDefault { get; set; }
    }

    public static SavedAddress Generate(SavedAddressPartial? mergeFrom = null)
    {
        var partial = mergeFrom ?? new SavedAddressPartial();
        var saved = SavedAddress.Create(
            userId: partial.UserId ?? Constants.TestUserSession.TestUserId,
            addressId: partial.AddressId ?? "address-1",
            label: partial.Label ?? "Home",
            isDefault: partial.IsDefault);
        saved.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        if (!string.IsNullOrEmpty(partial.Id))
        {
            saved.Id = partial.Id;
        }

        return saved;
    }
}
