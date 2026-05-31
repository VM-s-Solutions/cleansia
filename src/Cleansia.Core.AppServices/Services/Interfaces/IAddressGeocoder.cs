using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IAddressGeocoder
{
    Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken);
}
