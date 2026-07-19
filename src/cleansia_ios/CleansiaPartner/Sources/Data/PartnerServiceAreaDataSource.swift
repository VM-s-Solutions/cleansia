import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct PartnerServiceAreaDataSource: ServiceAreaDataSource {
    let client: PartnerProfileClient

    func fetchServicedCountries() async -> ApiResult<[ServicedCountry]> {
        await client.getServicedCountries().map { countries in
            countries.map {
                ServicedCountry(id: $0.id ?? "", isoCode: $0.isoCode ?? "", name: $0.name ?? "")
            }
        }
    }
}
