import CleansiaCore
import Combine
import Foundation

enum AddressManagerPane: Equatable {
    case list
    case addOnMap
    case reviewNew
}

@MainActor
final class AddressManagerViewModel: ViewModel {
    @Published private(set) var pane: AddressManagerPane = .list
    @Published private(set) var pickedAddress: GeocodedAddress?

    let repository: SavedAddressRepository
    let geocoding: GeocodingService
    let mapProvider: MapProvider

    private let snackbar: SnackbarController
    private var cancellables: Set<AnyCancellable> = []

    @Published private(set) var addresses: [SavedAddress] = []
    @Published private(set) var loaded = false

    init(
        repository: SavedAddressRepository,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        snackbar: SnackbarController
    ) {
        self.repository = repository
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.snackbar = snackbar
        super.init()
        repository.$addresses.assign(to: &$addresses)
        repository.$loaded.assign(to: &$loaded)
    }

    func onAppear() async {
        guard !repository.loading else { return }
        if case let .failure(error) = await repository.refresh() {
            snackbar.showApiError(error)
        }
    }

    func startAdd() {
        pickedAddress = nil
        pane = .addOnMap
    }

    func backToList() {
        pane = .list
    }

    func backToMap() {
        pane = .addOnMap
    }

    func mapDidConfirm(_ address: GeocodedAddress) {
        pickedAddress = address
        pane = .reviewNew
    }

    func saveReviewed(label: String, setAsDefault: Bool) async {
        guard let picked = pickedAddress else { return }
        let draft = picked.toDraft(label: trimmedLabel(label), setAsDefault: setAsDefault)
        if case let .failure(error) = await repository.add(draft) {
            snackbar.showApiError(error)
        }
        pickedAddress = nil
        pane = .list
    }

    func setDefault(id: String) async {
        if case let .failure(error) = await repository.setDefault(id: id) {
            snackbar.showApiError(error)
        }
    }

    func delete(id: String) async {
        if case let .failure(error) = await repository.delete(id: id) {
            snackbar.showApiError(error)
        }
    }

    func rename(id: String, newLabel: String) async {
        guard let existing = repository.addresses.first(where: { $0.id == id }),
              let latitude = existing.latitude,
              let longitude = existing.longitude
        else { return }
        let draft = SavedAddressDraft(
            label: trimmedLabel(newLabel),
            street: existing.street,
            city: existing.city,
            zipCode: existing.zipCode,
            country: existing.country,
            countryIsoCode: "",
            latitude: latitude,
            longitude: longitude,
            setAsDefault: existing.isDefault
        )
        if case let .failure(error) = await repository.update(id: id, draft: draft) {
            snackbar.showApiError(error)
        }
    }

    private func trimmedLabel(_ label: String) -> String {
        let trimmed = label.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? L10n.AddressManager.fallbackLabel : trimmed
    }
}
