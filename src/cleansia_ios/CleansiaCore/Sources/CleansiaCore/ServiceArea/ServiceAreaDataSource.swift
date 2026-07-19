import Foundation

/// App-side adapter contract — each app implements this over its own generated
/// serviced-countries call (`Country/GetServiced`) and maps the result to the
/// Core `ServicedCountry` shape. A failure comes back as the ADR-0011
/// `ApiResult` error, which the provider treats as UNKNOWN — never as "serves
/// nothing" — and does not cache.
public protocol ServiceAreaDataSource {
    func fetchServicedCountries() async -> ApiResult<[ServicedCountry]>
}
