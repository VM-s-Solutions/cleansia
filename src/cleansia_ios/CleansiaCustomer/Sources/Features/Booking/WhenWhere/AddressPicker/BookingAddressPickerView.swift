import CleansiaCore
import SwiftUI

/// The shared picker plus the booking's review step, which saves a new address before the booking takes it.
struct BookingAddressPickerView: View {
    @State private var saving = false

    @Environment(\.savedAddressRepository) private var repository
    @Environment(\.snackbarController) private var snackbar
    @Environment(\.bookingAddressSaveOffered) private var saveOffered

    private let geocoding: GeocodingService
    private let mapProvider: MapProvider
    private let serviceArea: ServiceAreaProvider?
    private let onConfirmed: (GeocodedAddress) -> Void
    private let onBack: () -> Void

    init(
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider? = nil,
        onConfirmed: @escaping (GeocodedAddress) -> Void,
        onBack: @escaping () -> Void
    ) {
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        // Kept, not just converted. The picker turns it into a geocoder search bias; the review
        // pane needs the provider itself to ask whether we actually operate in the city.
        self.serviceArea = serviceArea
        self.onConfirmed = onConfirmed
        self.onBack = onBack
    }

    var body: some View {
        AddressPickerScreen(
            geocoding: geocoding,
            mapProvider: mapProvider,
            serviceArea: serviceArea,
            copy: L10n.AddressPicker.self,
            review: saveOffered && repository != nil ? review : nil,
            onConfirmed: onConfirmed,
            onBack: onBack
        )
    }

    private func review(_ picked: GeocodedAddress, back: @escaping () -> Void) -> BookingAddressReviewPane {
        BookingAddressReviewPane(
            picked: picked,
            saving: saving,
            serviceArea: serviceArea,
            onBack: back,
            onConfirm: { label, save, setAsDefault in
                persist(picked, label: label, save: save, setAsDefault: setAsDefault)
            }
        )
    }

    private func persist(_ address: GeocodedAddress, label: String, save: Bool, setAsDefault: Bool) {
        guard
            let draft = BookingNewAddressSave.draft(
                from: address,
                label: label,
                save: save,
                setAsDefault: setAsDefault,
                fallbackLabel: L10n.AddressManager.fallbackLabel
            ),
            let repository
        else {
            onConfirmed(address)
            return
        }
        saving = true
        Task { @MainActor in
            if case let .failure(error) = await repository.add(draft) {
                snackbar.showApiError(error)
            }
            saving = false
            onConfirmed(address)
        }
    }
}

#if DEBUG
    struct BookingAddressPickerView_Previews: PreviewProvider {
        static var previews: some View {
            BookingAddressPickerView(
                geocoding: CLGeocoderGeocodingService(),
                mapProvider: PreviewMapProvider(),
                onConfirmed: { _ in },
                onBack: {}
            )
            .environment(\.locationProvider, PreviewLocationProvider())
        }
    }
#endif
