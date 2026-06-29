import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol CountryResolver {
    func countryId(forIsoCode isoCode: String) async -> String?
}

struct LiveCountryResolver: CountryResolver {
    func countryId(forIsoCode isoCode: String) async -> String? {
        guard !isoCode.isBlank else { return nil }
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerCountryAPI.countryGetServiced()
        }
        guard case let .success(countries) = result else { return nil }
        let target = isoCode.lowercased()
        return countries.first { ($0.isoCode?.lowercased() ?? "") == target }?.id
    }
}
