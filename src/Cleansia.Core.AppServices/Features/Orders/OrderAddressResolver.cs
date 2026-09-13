using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Default <see cref="IOrderAddressResolver"/>. Resolves the booking address (saved or inline) and
/// applies the serviced-area policy, then populates coordinates. Extracted verbatim from
/// <see cref="CreateOrder.Handler"/> — same ordering, same error codes.
/// </summary>
public sealed class OrderAddressResolver(
    IAddressRepository addressRepository,
    ISavedAddressRepository savedAddressRepository,
    ICountryRepository countryRepository,
    IServiceCityRepository serviceCityRepository,
    IAddressGeocoder addressGeocoder) : IOrderAddressResolver
{
    public async Task<OrderAddressResolution> ResolveAsync(
        CreateOrder.Command command, string userId, CancellationToken cancellationToken)
    {
        // Address resolution — saved vs inline. SavedAddressId path enforces
        // ownership so a user can't book against another user's saved row.
        var resolution = await ResolveAddressAsync(command, userId, cancellationToken);
        if (resolution.Failure is not null)
        {
            return resolution;
        }
        var address = resolution.Address!;

        // Customer orders must be in a city we actually serve. Employees
        // (UpdateEmployee / UpdateAddressInfo) are exempt — they can live
        // anywhere within a serviced country. The country itself has
        // already been validated as IsServiced by ResolveAddressAsync.
        if (!await serviceCityRepository.CityIsServicedAsync(
            address.CountryId, address.City, cancellationToken))
        {
            return OrderAddressResolution.Fail(new Error(
                nameof(address.City), BusinessErrorMessage.CityNotServiced));
        }

        if (address.Latitude is null || address.Longitude is null)
        {
            await addressGeocoder.PopulateCoordinatesAsync(address, cancellationToken);
        }

        return OrderAddressResolution.Ok(address);
    }

    public async Task<string?> ResolveCountryIdAsync(
        CreateOrder.Command command, string? userId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(command.SavedAddressId))
        {
            var saved = await savedAddressRepository.GetByIdAsync(command.SavedAddressId, cancellationToken);
            if (saved == null || string.IsNullOrEmpty(userId) || saved.UserId != userId)
            {
                return null;
            }
            var resolved = saved.Address
                ?? await addressRepository.GetByIdAsync(saved.AddressId, cancellationToken);
            return await ServicedOrNullAsync(resolved?.CountryId, cancellationToken);
        }

        var inlineCountryId = command.CustomerAddress?.CountryId;
        if (!string.IsNullOrEmpty(inlineCountryId))
        {
            return await ServicedOrNullAsync(inlineCountryId, cancellationToken);
        }

        var servicedCountries = await countryRepository.GetServicedAsync(cancellationToken);
        return servicedCountries.Count == 1 ? servicedCountries[0].Id : null;
    }

    /// <summary>
    /// Only a serviced country is a market. The currency resolver throws on a country it cannot
    /// resolve, and a country the platform does not operate in has no currency to ask for; the
    /// booking is refused as <c>CountryNotServiced</c> by <see cref="ResolveAsync"/> either way.
    /// </summary>
    private async Task<string?> ServicedOrNullAsync(string? countryId, CancellationToken cancellationToken)
        => !string.IsNullOrEmpty(countryId)
           && await countryRepository.IsServicedAsync(countryId, cancellationToken)
            ? countryId
            : null;

    private async Task<OrderAddressResolution> ResolveAddressAsync(
        CreateOrder.Command command, string userId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(command.SavedAddressId))
        {
            var saved = await savedAddressRepository.GetByIdAsync(command.SavedAddressId, cancellationToken);
            // A saved address belongs to a signed-in account. A guest has none, so a guest naming one is
            // naming somebody else's; NotFound, the same answer a wrong id or another user's id gets.
            if (saved == null || string.IsNullOrEmpty(userId) || saved.UserId != userId)
            {
                return OrderAddressResolution.Fail(new Error(
                    nameof(command.SavedAddressId), BusinessErrorMessage.NotFound));
            }
            var resolved = saved.Address
                ?? await addressRepository.GetByIdAsync(saved.AddressId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"SavedAddress {saved.Id} references missing Address {saved.AddressId}");

            // Edge case: address was saved while the country was
            // serviced, then admin de-flagged the country. Re-check
            // serviced status at order-time so stale saved rows can't
            // bypass the policy.
            if (!await countryRepository.IsServicedAsync(resolved.CountryId, cancellationToken))
            {
                return OrderAddressResolution.Fail(new Error(
                    nameof(command.SavedAddressId), BusinessErrorMessage.CountryNotServiced));
            }
            return OrderAddressResolution.Ok(resolved);
        }

        var inline = command.CustomerAddress!;
        var resolvedCountryId = inline.CountryId;

        // Country must be supplied AND must be one we operate in. Old
        // behaviour (alphabetically default to "first in catalog") was
        // the source of the Argentina-for-CZ-addresses bug — silently
        // picking a wrong default is worse than failing loud.
        if (!string.IsNullOrEmpty(resolvedCountryId)
            && !await countryRepository.IsServicedAsync(resolvedCountryId, cancellationToken))
        {
            return OrderAddressResolution.Fail(new Error(
                nameof(inline.CountryId), BusinessErrorMessage.CountryNotServiced));
        }
        if (string.IsNullOrEmpty(resolvedCountryId))
        {
            // No country supplied. Fall back to the single serviced
            // country if there's exactly one; otherwise require the
            // client to pick.
            var servicedCountries = await countryRepository.GetServicedAsync(cancellationToken);
            if (servicedCountries.Count != 1)
            {
                return OrderAddressResolution.Fail(new Error(
                    nameof(inline.CountryId), BusinessErrorMessage.CountryRequired));
            }
            resolvedCountryId = servicedCountries[0].Id;
        }

        var address = await addressRepository.GetAddressAsync(
            inline.Street, inline.City, inline.ZipCode, resolvedCountryId, cancellationToken)
            ?? Address.Create(inline.Street, inline.City, inline.ZipCode, resolvedCountryId, inline.State);
        return OrderAddressResolution.Ok(address);
    }
}
