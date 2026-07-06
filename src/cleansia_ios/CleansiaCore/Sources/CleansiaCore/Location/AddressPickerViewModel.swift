import Combine
import Foundation

@MainActor
public final class AddressPickerViewModel: ViewModel {
    @Published public private(set) var resolved: GeocodedAddress?
    @Published public private(set) var lookingUp = false
    @Published public private(set) var searchQuery = ""
    @Published public private(set) var searchResults: [GeocodedAddress] = []
    @Published public private(set) var searching = false

    public let confirmed = PassthroughSubject<GeocodedAddress, Never>()
    public let recenter = PassthroughSubject<Coordinate, Never>()

    public var canConfirm: Bool {
        resolved != nil && !lookingUp
    }

    private let geocoding: GeocodingService
    private let reverseDebounce: Duration
    private let searchDebounce: Duration
    private let searchBias: [String]
    private let servicedCountryCodesProvider: (() async -> [String]?)?
    private var resolvedBias: [String]?

    private var reverseTask: Task<Void, Never>?
    private var searchTask: Task<Void, Never>?

    /// `servicedCountryCodesProvider` is the serviced-countries seam (Android
    /// `ServiceAreaProvider.servicedCountryIsoCodes()` parity): when wired it
    /// wins over the static `searchBias` fallback. It returns nil when the
    /// fetch failed — that is UNKNOWN, not "serves nothing".
    public init(
        geocoding: GeocodingService,
        reverseDebounce: Duration = .milliseconds(500),
        searchDebounce: Duration = .milliseconds(300),
        searchBias: [String] = ["cz", "sk"],
        servicedCountryCodesProvider: (() async -> [String]?)? = nil
    ) {
        self.geocoding = geocoding
        self.reverseDebounce = reverseDebounce
        self.searchDebounce = searchDebounce
        self.searchBias = searchBias
        self.servicedCountryCodesProvider = servicedCountryCodesProvider
    }

    public func centerChanged(_ center: Coordinate) {
        lookingUp = true
        reverseTask?.cancel()
        reverseTask = Task { [weak self] in
            guard let self else { return }
            try? await Task.sleep(for: reverseDebounce)
            if Task.isCancelled { return }
            let address = await geocoding.reverseGeocode(center)
            if Task.isCancelled { return }
            if let address {
                resolved = address
            }
            lookingUp = false
        }
    }

    public func onSearchChange(_ query: String) {
        searchQuery = query
        searchTask?.cancel()
        guard query.count >= 2 else {
            searchResults = []
            searching = false
            return
        }
        searching = true
        searchTask = Task { [weak self] in
            guard let self else { return }
            try? await Task.sleep(for: searchDebounce)
            if Task.isCancelled { return }
            let bias = await currentSearchBias()
            if Task.isCancelled { return }
            let results = await geocoding.forwardGeocode(query: query, countryIsoCodes: bias)
            if Task.isCancelled { return }
            searchResults = results
            searching = false
        }
    }

    /// Only a successful provider answer is cached; nil (fetch failed) falls
    /// back to the static bias for this search and retries next time, so one
    /// startup blip never pins the fallback for the process lifetime.
    private func currentSearchBias() async -> [String] {
        if let resolvedBias { return resolvedBias }
        guard let servicedCountryCodesProvider else { return searchBias }
        guard let codes = await servicedCountryCodesProvider() else { return searchBias }
        let normalized = codes.map { IsoCountryCodes.toAlpha2($0) }.filter { !$0.isEmpty }
        resolvedBias = normalized
        return normalized
    }

    public func clearSearch() {
        onSearchChange("")
    }

    public func selectResult(_ address: GeocodedAddress) {
        searchTask?.cancel()
        resolved = address
        searchQuery = ""
        searchResults = []
        searching = false
        recenter.send(Coordinate(latitude: address.latitude, longitude: address.longitude))
    }

    public func confirm() {
        guard let resolved, !lookingUp else { return }
        confirmed.send(resolved)
    }
}
