import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct CustomerServiceAreaDataSource: ServiceAreaDataSource {
    func fetchServicedCountries() async -> ApiResult<[ServicedCountry]> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerCountryAPI.countryGetServiced()
        }
        return result.map { countries in
            countries.map {
                ServicedCountry(id: $0.id ?? "", isoCode: $0.isoCode ?? "", name: $0.name ?? "")
            }
        }
    }
}
