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

    private var reverseTask: Task<Void, Never>?
    private var searchTask: Task<Void, Never>?

    public init(
        geocoding: GeocodingService,
        reverseDebounce: Duration = .milliseconds(500),
        searchDebounce: Duration = .milliseconds(300),
        searchBias: [String] = ["cz", "sk"]
    ) {
        self.geocoding = geocoding
        self.reverseDebounce = reverseDebounce
        self.searchDebounce = searchDebounce
        self.searchBias = searchBias
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
            let results = await geocoding.forwardGeocode(query: query, countryIsoCodes: searchBias)
            if Task.isCancelled { return }
            searchResults = results
            searching = false
        }
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
