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
}
