import Foundation

/// Single source of truth for "which countries does the company serve" —
/// the Core port of Android `core/servicearea/ServiceAreaProvider`. Backs the
/// forward-geocode country bias and the partner Address section's advisory
/// country status. Fetched lazily on first access; ONLY a successful answer is
/// cached (in-memory, process lifetime — `refresh()` clears it). A failed
/// fetch is NOT cached, so the next access retries — caching the failure would
/// pin "serves nothing" for the process lifetime after one startup blip.
///
/// Cities are not exposed yet: the serviced-cities endpoint is absent from the
/// mobile specs, so the city-level half of the Android seam lands with the
/// spec regen (T-0334's gated remainder).
public actor ServiceAreaProvider {
    private let dataSource: ServiceAreaDataSource
    private var cached: [ServicedCountry]?
    private var inflight: Task<ApiResult<[ServicedCountry]>, Never>?

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

    public func refresh() {
        cached = nil
        inflight = nil
    }
}
