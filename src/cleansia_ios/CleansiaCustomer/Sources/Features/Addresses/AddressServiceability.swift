import CleansiaCore
import Foundation

/// Whether the platform operates in a picked address's city, for the two review panes that ask.
///
/// Extracted because a second caller now exists: the booking flow's own review pane had no check at
/// all, so a customer who added an address while booking heard nothing until payment — after
/// choosing a slot and seeing a price.
enum AddressServiceability {
    /// `nil` stays nil on a failed lookup, so nothing is claimed. Rendering "we do not serve here"
    /// because a request failed is how one startup blip makes every address look unserviceable.
    ///
    /// **Three outcomes, and they are not two.** A failed FETCH is unknown and says nothing. A
    /// country absent from a list we did fetch is a real no — `GetServiced` returns serviced
    /// countries only. A country we found narrows the question to the city.
    ///
    /// Both halves have been wrong once. The un-normalised ISO code made every country unfindable,
    /// and the fix for it wrongly folded "absent from the list" into the silent branch — so the
    /// banner went from showing on every address on earth to showing on none of them. The first is
    /// worse (a client refusing cities the server accepts is the one direction `CityNameMatch`
    /// forbids) but both are wrong, and they are distinguished here rather than in the caller.
    static func cityServiced(
        provider: ServiceAreaProvider?,
        countryIsoCode: String,
        city: String
    ) async -> Bool? {
        guard let provider, !city.isBlank else { return nil }
        guard let countries = await provider.loadCountries() else { return nil }

        // Both sides are alpha-2: the data source normalises the backend's alpha-3 through
        // IsoCountryCodes, and GeocodedAddress.countryIsoCode is already alpha-2 from CLPlacemark.
        let iso = IsoCountryCodes.toAlpha2(countryIsoCode)

        // A country ABSENT from a list we successfully fetched is a real answer: `GetServiced`
        // returns serviced countries only, so not being in it means we do not operate there.
        // `loadCountries()` already returned nil above if the fetch itself failed, which is the
        // genuinely unknown case — the two must not collapse into one silent branch, and briefly
        // did: San Francisco showed nothing at all.
        guard let country = countries.first(where: { $0.isoCode == iso }) else {
            return false
        }

        return await provider.isCityServiced(countryId: country.id, cityName: city)
    }
}
