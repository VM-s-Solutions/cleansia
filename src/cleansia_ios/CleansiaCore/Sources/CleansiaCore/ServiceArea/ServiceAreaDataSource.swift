import Foundation

/// App-side adapter contract — each app implements this over its own generated
/// serviced-countries call (`Country/GetServiced`) and maps the result to the
/// Core `ServicedCountry` shape. A failure comes back as the ADR-0011
/// `ApiResult` error, which the provider treats as UNKNOWN — never as "serves
/// nothing" — and does not cache.
public protocol ServiceAreaDataSource {
    func fetchServicedCountries() async -> ApiResult<[ServicedCountry]>

    /// Cities the company serves, optionally narrowed to one country. Same UNKNOWN-on-failure contract
    /// as the countries call: a failure is "could not check", never "serves nowhere".
    func fetchServicedCities(countryId: String?) async -> ApiResult<[ServicedCity]>
}

public extension ServiceAreaDataSource {
    /// Defaults to UNKNOWN, so an app that has no reason to ask does not have to answer.
    ///
    /// Only the CUSTOMER books, so only the customer app needs city-level serviceability; the partner
    /// side shows country status and nothing finer. The default is deliberately a failure rather than
    /// an empty list — empty would mean "serves nowhere", which every caller is required to render as a
    /// refusal, and that is the one answer this must never invent.
    func fetchServicedCities(countryId _: String?) async -> ApiResult<[ServicedCity]> {
        .failure(ApiError(code: "servicearea.cities_not_queried"))
    }
}
