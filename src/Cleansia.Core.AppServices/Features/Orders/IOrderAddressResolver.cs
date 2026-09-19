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

    /// <summary>
    /// The country the booking's address is in, read the same way <see cref="ResolveAsync"/> reads it
    /// but without the serviced-area gates or the geocode: the validator needs it before the handler
    /// runs, because the order's currency is that country's currency and every price and pay rule is
    /// asked in it. Null when the command does not determine one — a missing or foreign saved row, an
    /// inline address with no country in a platform that services more than one, or a country the
    /// platform does not service — which <see cref="ResolveAsync"/> then refuses with its own code.
    /// Only a serviced country is answered: the currency resolver throws on a country it cannot
    /// resolve, and an unserviced one has no currency to ask for.
    /// </summary>
    Task<string?> ResolveCountryIdAsync(
        CreateOrder.Command command, string? userId, CancellationToken cancellationToken);
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
