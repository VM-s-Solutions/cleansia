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
                // Alpha-2 at the BOUNDARY, matching Android. The backend stores alpha-3 ("CZE");
                // everything geocoder-facing is alpha-2 ("cz"), so an un-normalised code can never
                // match and every caller silently concludes "country not serviced".
                ServicedCountry(
                    id: $0.id ?? "",
                    isoCode: IsoCountryCodes.toAlpha2($0.isoCode),
                    name: $0.name ?? ""
                )
            }
        }
    }

    func fetchServicedCities(countryId: String?) async -> ApiResult<[ServicedCity]> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerServiceCityAPI.serviceCityGetServiceCities(countryId: countryId)
        }
        // A row with no id or no name cannot be matched against and cannot be rendered, so it is
        // dropped rather than carried as an empty string that would silently match a blank city.
        return result.map { cities in
            cities.compactMap { city in
                guard let id = city.id, let name = city.name, !name.isBlank,
                      let country = city.countryId
                else { return nil }
                return ServicedCity(id: id, countryId: country, name: name)
            }
        }
    }
}
