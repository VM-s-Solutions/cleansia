import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol CountryResolver {
    func countryId(forIsoCode isoCode: String) async -> String?
}

struct LiveCountryResolver: CountryResolver {
    func countryId(forIsoCode isoCode: String) async -> String? {
        // Geocoder gives alpha-2 ("cz"), backend stores alpha-3 ("CZE") —
        // both sides normalise through the Core table or they never match.
        let target = IsoCountryCodes.toAlpha2(isoCode)
        guard !target.isEmpty else { return nil }
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerCountryAPI.countryGetServiced()
        }
        guard case let .success(countries) = result else { return nil }
        return countries.first { IsoCountryCodes.toAlpha2($0.isoCode) == target }?.id
    }
}
