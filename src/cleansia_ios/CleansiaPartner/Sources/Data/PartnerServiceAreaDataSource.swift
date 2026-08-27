import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct PartnerServiceAreaDataSource: ServiceAreaDataSource {
    let client: PartnerProfileClient

    func fetchServicedCountries() async -> ApiResult<[ServicedCountry]> {
        await client.getServicedCountries().map { countries in
            countries.map {
                // Alpha-2 at the BOUNDARY — see the customer twin.
                ServicedCountry(
                    id: $0.id ?? "",
                    isoCode: IsoCountryCodes.toAlpha2($0.isoCode),
                    name: $0.localizedName()
                )
            }
        }
    }

    /// Without this the Core protocol's default answered `.failure` forever, so the city
    /// rule was never merely wrong — it was never asked. Shape copied from the customer
    /// twin, including the part that matters: a failure is NOT flattened to an empty
    /// list, because "we could not check" and "there are no serviced cities" must stay
    /// distinguishable all the way to the view.
    func fetchServicedCities(countryId: String?) async -> ApiResult<[ServicedCity]> {
        await client.getServiceCities(countryId: countryId).map { cities in
            cities.compactMap { city in
                guard let id = city.id, let name = city.name, !name.isBlank,
                      let country = city.countryId
                else { return nil }
                return ServicedCity(id: id, countryId: country, name: name)
            }
        }
    }
}
