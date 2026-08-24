import Foundation

/// Single source of truth for "which countries does the company serve" —
/// the Core port of Android `core/servicearea/ServiceAreaProvider`. Backs the
/// forward-geocode country bias and the partner Address section's advisory
/// country status. Fetched lazily on first access; ONLY a successful answer is
/// cached (in-memory, process lifetime — `refresh()` clears it). A failed
/// fetch is NOT cached, so the next access retries — caching the failure would
/// pin "serves nothing" for the process lifetime after one startup blip.
///
/// Cities are exposed too, and answer the question the customer used to discover at PAYMENT: the
/// booking gate refuses an address outside a serviced city, and until this existed on iOS nothing
/// asked earlier. The match is `CityNameMatch`, the same rule `CreateOrder` applies — a client that
/// were stricter would turn away addresses the server would book.
public actor ServiceAreaProvider {
    private let dataSource: ServiceAreaDataSource
    private var cached: [ServicedCountry]?
    private var inflight: Task<ApiResult<[ServicedCountry]>, Never>?
    private var cachedCities: [ServicedCity]?
    private var inflightCities: Task<ApiResult<[ServicedCity]>, Never>?

    public init(dataSource: ServiceAreaDataSource) {
        self.dataSource = dataSource
    }

    public func loadCountriesResult() async -> ApiResult<[ServicedCountry]> {
        if let cached { return .success(cached) }
        if let inflight { return await inflight.value }
        let task = Task { [dataSource] in await dataSource.fetchServicedCountries() }
        inflight = task
        let result = await task.value
        inflight = nil
        if case let .success(countries) = result {
            cached = countries
        }
        return result
    }

    /// nil = the fetch failed and the answer is UNKNOWN — treat it as
    /// "couldn't check", never as "serves nothing".
    public func loadCountries() async -> [ServicedCountry]? {
        try? await loadCountriesResult().get()
    }

    /// Alpha-2 lowercase codes for the forward-geocode bias. nil = UNKNOWN so
    /// `AddressPickerViewModel` keeps its static fallback and retries next
    /// search instead of pinning an empty bias.
    public func servicedCountryIsoCodes() async -> [String]? {
        await loadCountries()?
            .map { IsoCountryCodes.toAlpha2($0.isoCode) }
            .filter { !$0.isEmpty }
    }

    /// Every serviced city, unnarrowed and cached — the caller filters. Fetching per country would
    /// re-request on every address the customer looks at in a different country, and the whole list is
    /// tens of rows.
    public func loadCities() async -> [ServicedCity]? {
        if let cachedCities { return cachedCities }
        if let inflightCities { return try? await inflightCities.value.get() }
        let task = Task { [dataSource] in await dataSource.fetchServicedCities(countryId: nil) }
        inflightCities = task
        let result = await task.value
        inflightCities = nil
        if case let .success(cities) = result {
            cachedCities = cities
        }
        return try? result.get()
    }

    /// `nil` means UNKNOWN — the list could not be fetched. **A caller must not render that as "we do
    /// not serve here"**: warning on a failed lookup is how a startup blip turns into every address
    /// looking unserviceable.
    public func isCityServiced(countryId: String, cityName: String) async -> Bool? {
        guard let cities = await loadCities() else { return nil }
        let inCountry = cities.filter { $0.countryId == countryId }.map(\.name)
        return CityNameMatch.isServiced(inCountry, cityName)
    }

    public func refresh() {
        cached = nil
        inflight = nil
        cachedCities = nil
        inflightCities = nil
    }
}
