using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Resolves the booking <see cref="Address"/> for a <see cref="CreateOrder.Command"/> and gates it
/// against the serviced-area policy. Owns the three concerns lifted out of
/// <see cref="CreateOrder.Handler"/>: saved-vs-inline resolution with saved-row ownership,
/// the serviced country/city checks, and coordinate population.
///
/// The contract preserves the handler's original step ordering and error codes exactly:
/// address resolve (NotFound / CountryNotServiced / CountryRequired) → city gate (CityNotServiced)
/// → geocode. A successful result carries an <see cref="Address"/> with coordinates populated.
/// </summary>
public interface IOrderAddressResolver
{
    Task<OrderAddressResolution> ResolveAsync(
        CreateOrder.Command command, string userId, CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of <see cref="IOrderAddressResolver.ResolveAsync"/>: either a resolved booking
/// <see cref="Address"/> or the <see cref="Error"/> the handler returns as a failure.
/// </summary>
public record OrderAddressResolution(Address? Address, Error? Failure)
{
    public static OrderAddressResolution Ok(Address address) => new(address, null);
    public static OrderAddressResolution Fail(Error error) => new(null, error);
}
